# Graph Report - .  (2026-08-23)

## Corpus Check
- Large corpus: 786 files · ~738,792 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder, or use --no-semantic to run AST-only.

## Summary
- 6732 nodes · 15969 edges · 305 communities detected
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output
- Edge kinds: calls: 4299 · contains: 3152 · MODIFIES: 2385 · method: 2384 · imports: 1571 · ON_BRANCH: 800 · imports_from: 458 · PARENT_OF: 390 · references: 378 · inherits: 87 · reads_from: 39 · triggers: 20 · re_exports: 6


## Input Scope
- Requested: committed
- Resolved: committed (source: cli)
- Included files: 786 · Candidates: 985
- Excluded: 6 untracked · 48153 ignored · 1 sensitive · 0 missing committed
- Recommendation: Use --scope all or graphify.yaml inputs.corpus for a knowledge-base folder.

## Graph Freshness
- Built from Git commit: `3321b17`
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
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 235 → community 127_
- `appointments` --references--> `patients`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 127 → community 89_
- `inventory_lots` --references--> `facilities`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 206 → community 127_
- `lab_orders` --references--> `patients`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 168 → community 89_
- `lab_orders` --references--> `staff`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 168 → community 127_

## Communities

### Community 0 - "Community 0"
Cohesion: 0.01
Nodes (321): announceInvalidSession(), ApiErrorKind, apiFetch(), ApiProblemDetails, ApiRequestError, isInvalidSessionError(), isRequestCancellation(), materializeRequestHeaders() (+313 more)

### Community 1 - "Community 1"
Cohesion: 0.01
Nodes (105): 077c7bc fix(clinical): block new content on inactive patients, 08d116e Establish hybrid EF Core data access foundation, 0b706a0 Split clinical list state into EF Core, 4d9e669 Move referral workflow state to EF Core, 5d1561c fix(therapy): atomically link generated encounters, 6863261 Adopt EF Core for directory and education data, 6a1c3d4 feat(therapy): record session attendance in modern UI, 73c3e09 Split procedure directory state into EF Core (+97 more)

### Community 2 - "Community 2"
Cohesion: 0.05
Nodes (129): codex/ef-data-access-modernization, main, 0220d35 fix(auth): scope scheduling and encounter workflows to facility, 0239c9f fix(labs): invalidate stale critical result queue, 05beda1 refactor(api): isolate encounter endpoints, 05fa4fb fix(patients): make administration updates atomic, 07e8fdd fix(sdoh): anchor generated goals to assessment date, 083e6b1 fix(scheduling): enforce appointment concurrency (+121 more)

### Community 3 - "Community 3"
Cohesion: 0.02
Nodes (97): ClinicianMessages(), FilterDraft, filtersFromParams(), initials(), PatientThread, queryFromParams(), ThreadPatient, ThreadState (+89 more)

### Community 4 - "Community 4"
Cohesion: 0.02
Nodes (92): asEncounterLifecycleDetail(), EncounterSoapNoteConflict, EncounterSoapNoteConflictProblem, EncounterSoapNoteVersion, getEncounterSoapNoteConflict(), getVersionedEncounterDetail(), saveEncounterSoapNote(), SaveEncounterSoapNoteInput (+84 more)

### Community 5 - "Community 5"
Cohesion: 0.03
Nodes (19): 04b5c83 feat(therapy): require recorded group attendance, 431b12a chore: checkpoint AvenChart workspace, 4f39cd9 Publish AvenChart source and public history, 56a8a1a Adopt AvenChart product identity, 58f7374 Harden migrations and add review assessments, FieldDefinition, PatientEducationRepository, ClinicalAlertSeverity (+11 more)

### Community 6 - "Community 6"
Cohesion: 0.05
Nodes (86): codex/appointment-scheduling, codex/local-docker-scripts, 0316b13 feat(procedures): protect locked encounter order entry, 05ca2d7 feat(encounters): type locked track catalog, 07ec116 feat(admin): govern facility settings, 0fb4655 feat(labs): govern specimen lifecycle, 10b1502 feat(ui): add referral work queue, 10f5fc4 feat(billing): protect locked charge mutations (+78 more)

### Community 7 - "Community 7"
Cohesion: 0.06
Nodes (1): BillingRepository

### Community 8 - "Community 8"
Cohesion: 0.07
Nodes (1): DocumentRepository

### Community 9 - "Community 9"
Cohesion: 0.02
Nodes (86): actions, Draft, statuses, actions, Draft, actions, FormLayoutDefinitionField, FormLayoutDefinitionGroup (+78 more)

### Community 10 - "Community 10"
Cohesion: 0.02
Nodes (77): ExportEvidence, formatFilterSummary(), InventoryActivityPanel(), Props, ReportFilters, ReportRun, CatalogState, Props (+69 more)

### Community 11 - "Community 11"
Cohesion: 0.02
Nodes (84): AccessMembershipForm, AccessPermissionForm, ApiClientForm, AsyncState, CodingCatalogForm, FacilityForm, UserForm, acceptAdministrationPortalProfileReview() (+76 more)

### Community 12 - "Community 12"
Cohesion: 0.06
Nodes (1): PatientRepository

### Community 13 - "Community 13"
Cohesion: 0.07
Nodes (1): AppointmentRepository

### Community 14 - "Community 14"
Cohesion: 0.07
Nodes (1): ProcedureRepository

### Community 15 - "Community 15"
Cohesion: 0.02
Nodes (1): af0f321 fix(validation): harden runtime workflow evidence

### Community 16 - "Community 16"
Cohesion: 0.05
Nodes (73): amendClinicalFormInstance(), ClinicalFormCondition, ClinicalFormDefinitionDetail, ClinicalFormDefinitionEvent, ClinicalFormDefinitionList, ClinicalFormEvaluation, ClinicalFormFieldDictionary, ClinicalFormFieldDictionaryItem (+65 more)

### Community 17 - "Community 17"
Cohesion: 0.03
Nodes (57): CatalogForm, CatalogState, DirectoryState, LabReportAndResultCapture(), Props, today(), AsyncState, PatientLabs() (+49 more)

### Community 18 - "Community 18"
Cohesion: 0.04
Nodes (39): 022ba1c feat(forms): adopt legacy bronchitis sinus exam, 0383578 feat(forms): adopt legacy clinical instructions, 13b3e65 feat(forms): adopt legacy review systems pulmonary, 197a8b2 feat(forms): adopt legacy bronchitis cardiac exam, 1b619ab feat(forms): adopt legacy ankle diagnosis plan, 20b406c feat(forms): adopt legacy review systems endocrine, 2a6434a feat(forms): adopt legacy clinic note, 2d2b483 feat(forms): add governed clinical form engine (+31 more)

### Community 19 - "Community 19"
Cohesion: 0.03
Nodes (65): AppErrorBoundary, Component, createErrorReference(), Props, State, AddressBook, AdminDirectory, AuthorizationWorkQueue (+57 more)

### Community 20 - "Community 20"
Cohesion: 0.06
Nodes (61): cancelGovernedReportRun(), createGovernedReportDefinition(), createGovernedReportRevision(), deleteGovernedReportDefinitionTestFixture(), downloadGovernedReportRun(), getGovernedReportCatalog(), getGovernedReportDefinition(), getGovernedReportDefinitions() (+53 more)

### Community 21 - "Community 21"
Cohesion: 0.04
Nodes (47): CriticalLabResultFollowUpLifecycleTests, 0a3a419 Split encounter state mutations into EF Core, 11c91a9 fix(labs): bind report reviews to result content, 1e2a01d Replace global integer allocators with sequences, 21f29da fix(encounters): reject stale summary updates, 32bb53e feat(labs): govern local report review lifecycle, 4316e88 fix(ui): make flow board refreshes safe, 47505bf fix(ui): prevent stale patient chart responses (+39 more)

### Community 22 - "Community 22"
Cohesion: 0.03
Nodes (57): administrationAreaLabels, administrationFieldLabels, BLANK_INS, CareTeamDraft, CareTeamMemberDraft, careTeamRoleOptions, careTeamStatusOptions, fact() (+49 more)

### Community 23 - "Community 23"
Cohesion: 0.09
Nodes (2): DiagnosisAccumulator, EncounterRepository

### Community 24 - "Community 24"
Cohesion: 0.06
Nodes (37): BrowserOidcAudience, BrowserOidcConfiguration, BrowserOidcPortalSession, BrowserOidcStaffSession, getBrowserOidcConfiguration(), getBrowserOidcPortalSession(), getBrowserOidcStaffSession(), startBrowserOidcSignIn() (+29 more)

### Community 25 - "Community 25"
Cohesion: 0.04
Nodes (35): ClinicianSession, loadClinicianSession(), updateClinicianSession(), AddressBook(), blank(), Draft, State, validStatuses (+27 more)

### Community 26 - "Community 26"
Cohesion: 0.05
Nodes (1): PatientPortalRepository

### Community 27 - "Community 27"
Cohesion: 0.09
Nodes (1): ClinicalFormRepository

### Community 28 - "Community 28"
Cohesion: 0.04
Nodes (50): empty, formatDate(), InventoryControlledCountsPanel(), Props, addRecallActivity(), approveInventoryControlledCountAttestation(), approveInventoryControlledDiscrepancyCorrectionAttestation(), BatchCommunicationFilter (+42 more)

### Community 29 - "Community 29"
Cohesion: 0.04
Nodes (39): displayDate(), DraftForm, emptyDraft, MutableAuthorizationState, PatientAuthorizations(), titleCase(), PrintableOutput, DOMAINS (+31 more)

### Community 30 - "Community 30"
Cohesion: 0.07
Nodes (1): AdministrationRepository

### Community 31 - "Community 31"
Cohesion: 0.09
Nodes (1): ClinicalFormRuntime

### Community 32 - "Community 32"
Cohesion: 0.04
Nodes (39): AsyncState, ClinicianDashboard(), greeting(), RecentPatient, today(), ActionEditor, DocumentOcrQueue(), HistoryState (+31 more)

### Community 33 - "Community 33"
Cohesion: 0.10
Nodes (37): ClinicalFormCalculation, ClinicalFormCalculationTemplate, ClinicalFormField, ClinicalFormPolicy, ClinicalFormRule, actionsFor(), ClinicalFormGovernance(), Props (+29 more)

### Community 34 - "Community 34"
Cohesion: 0.06
Nodes (19): BackgroundService, AsyncState, 10151e8 feat(reports): add governed clinical form reporting, 180ca5a feat(reports): add governed local execution, 97afd2a feat(reports): enforce local report row scope, a0cfd08 feat(reports): govern definition catalog, a7745fa feat(reports): add local operations console, b42afbf feat(reports): add durable execution lifecycle (+11 more)

### Community 35 - "Community 35"
Cohesion: 0.08
Nodes (6): IDisposable, IHostedService, AzureCliRunner, AzureDeploymentCoordinator, AzureOperationsService, TemporaryParameterFile

### Community 36 - "Community 36"
Cohesion: 0.11
Nodes (1): ReportExecutionRepository

### Community 37 - "Community 37"
Cohesion: 0.15
Nodes (1): ClinicalListRepository

### Community 38 - "Community 38"
Cohesion: 0.06
Nodes (13): AccessibilityFinding, clinicianFixture, codingEncounter, encounter, cleanupLifecycleFixture(), composeRoot, fixtureSql(), AvenChartUiFixtures (+5 more)

### Community 39 - "Community 39"
Cohesion: 0.09
Nodes (35): archiveAzureDeploymentProfile(), assessAzureDeploymentProfile(), AzureAccessValidationResponse, AzureDeploymentExecutionDetail, AzureDeploymentExecutionSummary, AzureDeploymentHealth, AzureDeploymentProfileAssessment, AzureDeploymentProfileDetail (+27 more)

### Community 40 - "Community 40"
Cohesion: 0.08
Nodes (36): actOnManagedRecord(), createManagedRecord(), deleteManagedRecordTestFixture(), getManagedRecordHistory(), getManagedRecordPolicy(), getManagedRecords(), headers(), ManagedRecordCreateInput (+28 more)

### Community 41 - "Community 41"
Cohesion: 0.15
Nodes (23): ArgumentException, ExternalLaboratoryFhirValidationException, ExternalLaboratoryIntakeRepository, FirstReference(), Invalid(), Matches(), Parse(), ParseObservation() (+15 more)

### Community 42 - "Community 42"
Cohesion: 0.13
Nodes (1): MessageRepository

### Community 43 - "Community 43"
Cohesion: 0.06
Nodes (26): formatCurrency(), InventoryDispensingPanel(), LotWithItem, PatientContextState, PatientSearchState, Props, Result, ALL_KINDS (+18 more)

### Community 44 - "Community 44"
Cohesion: 0.08
Nodes (35): api_client_registry, auth_audit_events, clinical_alert_rules, coding_catalog_audit_events, coding_catalogs, dataset_metadata, encounter_audit_events, encounter_clinical_alert_acknowledgments (+27 more)

### Community 45 - "Community 45"
Cohesion: 0.08
Nodes (18): 0819a56 feat(auth): add provider-neutral OIDC and test IdP, 231a478 feat(auth): govern external OIDC subject mappings, 74af4ef fix(labs): serialize procedure result corrections, aa093cf Harden migration startup and schema readiness, ab3a3f9 feat(portal): support governed OIDC identity mappings, b03f736 fix(labs): make facility intake context explicit, bea1383 feat(database): bootstrap empty PostgreSQL schemas, f33a442 fix(migrations): harden empty database recovery (+10 more)

### Community 46 - "Community 46"
Cohesion: 0.08
Nodes (21): 110de8a style(backend): apply C# formatter, 4183296 feat(patients): audit deceased status corrections, 5dda587 fix(prescriptions): retain records and audit evidence, 72874b1 feat(clinical): retain immutable list mutation evidence, 737f5b7 fix(prescriptions): block continuation after lifecycle closure, a8cdf66 feat(patients): govern retirement lifecycle, f4268e0 feat(clinical): add medication lifecycle restore history, f858e05 feat(clinical): add medication content edit history (+13 more)

### Community 47 - "Community 47"
Cohesion: 0.09
Nodes (2): InventoryItemBuilder, InventoryRepository

### Community 48 - "Community 48"
Cohesion: 0.07
Nodes (17): getIdentityProviderReadiness(), IdentityAdapterContract, IdentityBoundaryControl, IdentityProviderGap, IdentityProviderReadiness, IdentityProviderReadinessCounts, IdentityTypeReadiness, IdentityVerification (+9 more)

### Community 49 - "Community 49"
Cohesion: 0.13
Nodes (28): createPatientDisclosureAuthority(), createPatientDisclosureRequest(), decidePatientDisclosureRequest(), getPatientDisclosureAuthorities(), getPatientDisclosureAuthorityHistory(), getPatientDisclosurePolicy(), getPatientDisclosureRequestHistory(), getPatientDisclosureRequests() (+20 more)

### Community 50 - "Community 50"
Cohesion: 0.15
Nodes (1): ManagedRecordRepository

### Community 51 - "Community 51"
Cohesion: 0.10
Nodes (33): appointment_reminder_dispatch_audit, avenchart_integer_counters, lab_results, medication_vocabulary, patient_administration_audit_events, patient_document_archive_events, patient_document_content_events, patient_document_metadata_events (+25 more)

### Community 52 - "Community 52"
Cohesion: 0.12
Nodes (1): ReportRepository

### Community 54 - "Community 54"
Cohesion: 0.15
Nodes (1): BrowserOidcSessionService

### Community 55 - "Community 55"
Cohesion: 0.15
Nodes (1): ReportDefinitionRepository

### Community 56 - "Community 56"
Cohesion: 0.06
Nodes (25): accessGroupPermissions, accessGroups, accessPermissions, accessUserMemberships, allNonPlaceholderPermissions, bootstrapOnly, bootstrapPath, copyEmptyString (+17 more)

### Community 57 - "Community 57"
Cohesion: 0.08
Nodes (25): AddMode, AsyncState, ClinicalAuditHistoryState, ClinicalAuditResourceType, LifecycleTarget, PatientChart(), today(), VocabularyState (+17 more)

### Community 58 - "Community 58"
Cohesion: 0.10
Nodes (19): AppointmentEditForm, AsyncState, DURATION_OPTIONS, formatTime(), formFromAppointment(), PatientAppointments(), todayStr(), AppointmentEditForm (+11 more)

### Community 59 - "Community 59"
Cohesion: 0.14
Nodes (3): Allowed(), Denied(), StaffAccessContextService

### Community 61 - "Community 61"
Cohesion: 0.09
Nodes (16): AsyncState, SUBJECT_PRESETS, View, archivePatientPortalMessages(), composePatientPortalMessage(), deletePatientPortalMessage(), downloadPatientPortalMessageAttachment(), getPatientPortalMessageComposeOptions() (+8 more)

### Community 62 - "Community 62"
Cohesion: 0.13
Nodes (18): getPatientPortalAppointmentsWithRequestHistory(), PatientPortalAppointmentRequestHistoryEvent, PatientPortalAppointmentRequestHistoryItem, PatientPortalAppointmentsWithRequestHistoryResponse, 1a4aff4 feat(portal): add appointment request history, AppointmentCard(), AsyncState, buildIcsContent() (+10 more)

### Community 63 - "Community 63"
Cohesion: 0.09
Nodes (21): DetailState, DocumentTemplates(), formatDateTime(), pageCount(), selectedPage(), TemplateDraft, createDocumentTemplate(), DocumentTemplateBinaryVersion (+13 more)

### Community 64 - "Community 64"
Cohesion: 0.08
Nodes (24): AsyncState, formatDateTime(), PracticeSettingGovernance(), Props, statusBadgeClass(), statusLabels, createPracticeSettingChangeRequest(), EffectivePracticeSettingItem (+16 more)

### Community 65 - "Community 65"
Cohesion: 0.21
Nodes (1): AuthorizationRepository

### Community 66 - "Community 66"
Cohesion: 0.18
Nodes (1): IntegrationIdempotencyConflictException

### Community 67 - "Community 67"
Cohesion: 0.22
Nodes (1): PatientDisclosureRepository

### Community 68 - "Community 68"
Cohesion: 0.11
Nodes (7): 06cf8a3 feat(fhir): validate external laboratory R4 profiles, 2a53ba9 feat(labs): scope external sources to facilities, 32d97a0 feat(labs): ingest profiled FHIR laboratory results, a9eec9f fix(fhir): make search contract pageable and typed, bc6cc4d feat(labs): govern external laboratory source credentials, e474fa1 fix(fhir): omit empty optional repeat fields, FhirResults

### Community 70 - "Community 70"
Cohesion: 0.15
Nodes (1): AzureOperationsRepository

### Community 71 - "Community 71"
Cohesion: 0.28
Nodes (1): ClinicalListStateRepository

### Community 72 - "Community 72"
Cohesion: 0.23
Nodes (1): FhirRepository

### Community 73 - "Community 73"
Cohesion: 0.14
Nodes (3): PatientEncounterAccumulator, PatientTrackAccumulator, TrackAnythingRepository

### Community 74 - "Community 74"
Cohesion: 0.16
Nodes (1): AzureDeploymentProfilePolicy

### Community 75 - "Community 75"
Cohesion: 0.13
Nodes (22): ClinicalFormSchema, ClinicalFormSection, 5ceebdc feat(admin): show api client request evidence, 9645171 feat(forms): explain successor change impact, canonicalJson(), ClinicalFormChangeImpact, ClinicalFormImpactItem, ClinicalFormImpactSeverity (+14 more)

### Community 76 - "Community 76"
Cohesion: 0.11
Nodes (20): asEncounterCodingDetail(), BillingLineCreateInput, clinicianHeaders(), CompleteEncounterCreateInput, createCompleteEncounter(), createEncounterBillingLine(), EncounterBillingClaim, EncounterBillingLine (+12 more)

### Community 77 - "Community 77"
Cohesion: 0.13
Nodes (9): 0701dc1 Merge pull request #1 from nkimber/codex/local-docker-scripts, 286a7d3 Add local Docker management scripts, 58e8ddb Clarify autonomous OpenEMR rewrite, 598cdf2 Add protected Azure deployment operations, 8b1d203 Improve local setup and history timeline, ce59034 Add OpenEMR functional coverage estimate, d28d9e2 Clarify autonomous rewrite experiment, AzureOperationsOptions (+1 more)

### Community 79 - "Community 79"
Cohesion: 0.24
Nodes (1): ReferralRepository

### Community 80 - "Community 80"
Cohesion: 0.17
Nodes (17): archiveEncounterWithReason(), changeEncounterArchiveState(), clinicianHeaders(), EncounterLifecycleConflictError, EncounterLifecycleDetail, EncounterSignInput, lifecycleFetch(), requireArchiveVersion() (+9 more)

### Community 82 - "Community 82"
Cohesion: 0.16
Nodes (1): DocumentTemplateRepository

### Community 83 - "Community 83"
Cohesion: 0.22
Nodes (1): PatientMergeExecutionRepository

### Community 84 - "Community 84"
Cohesion: 0.20
Nodes (2): ReportExecutionQueueRepository, WorkerCancellationState

### Community 85 - "Community 85"
Cohesion: 0.13
Nodes (18): BillingWorkspace(), money(), context, CollectionsWorkQueueResponse, createBillingAdjustmentReversal(), createBillingCollectionsFollowUp(), createBillingInsurancePayment(), createBillingInsuranceReversal() (+10 more)

### Community 86 - "Community 86"
Cohesion: 0.13
Nodes (10): 2b91a4a feat(integrations): govern outbox recovery, 66cc16f feat(integrations): govern inbox reconciliation, 9fd53c1 feat(integrations): recover expired dispatch leases, d420fed feat(integrations): expose inbox decision history, IIntegrationTransport, LocalDeterministicIntegrationTransport, integration_outbox, integration_outbox_events (+2 more)

### Community 88 - "Community 88"
Cohesion: 0.22
Nodes (1): InventoryReplenishmentPolicyRepository

### Community 89 - "Community 89"
Cohesion: 0.13
Nodes (20): allergies, claims, clinical_notes, insurance_records, messages, patient_employers, patient_histories, patient_portal_accounts (+12 more)

### Community 92 - "Community 92"
Cohesion: 0.19
Nodes (6): IEndpointFilter, AzureOperationsAccessFilter, AzureOperationsAccessLockedException, AzureOperationsAccessService, AzureOperationsEnabledFilter, UnauthorizedAccessException

### Community 93 - "Community 93"
Cohesion: 0.15
Nodes (8): CreateOptions, FixtureCleanup, FixtureId, FixtureReset, LifecycleDomain, lifecycleDomains, LifecycleFixtureRegistry, LifecycleRecord

### Community 94 - "Community 94"
Cohesion: 0.12
Nodes (8): ClinicianCalendar(), isoDate(), PALETTE, ProviderEntry, WEEKDAYS, AppointmentStatusBadge(), AsyncState, downloadPatientPortalGeneratedMedicalReportPdf()

### Community 95 - "Community 95"
Cohesion: 0.15
Nodes (14): addTherapyGroupMember(), createTherapyGroup(), createTherapyGroupSession(), createTherapyGroupSessionEncounters(), getTherapyGroupMembers(), getTherapyGroups(), getTherapyGroupSessionAttendance(), getTherapyGroupSessions() (+6 more)

### Community 96 - "Community 96"
Cohesion: 0.18
Nodes (15): 2e2dbd9 feat(inventory): support specific identification costing, 6066464 feat(inventory): restore linked cost layers, 6268ce2 feat(inventory): support specific transfer costing, 69844bd feat(inventory): reallocate FIFO transfer costs, 7002695 feat(inventory): reallocate weighted transfer costs, 74ad7be feat(inventory): record costing exceptions, 9ea2933 feat(inventory): show cost layer applications, a006584 feat(inventory): cost patient sale movements (+7 more)

### Community 97 - "Community 97"
Cohesion: 0.21
Nodes (1): AdministrationDirectoryRepository

### Community 99 - "Community 99"
Cohesion: 0.21
Nodes (2): AuthRepository, ToResponse()

### Community 100 - "Community 100"
Cohesion: 0.25
Nodes (1): InventoryCostPolicyRepository

### Community 102 - "Community 102"
Cohesion: 0.26
Nodes (1): ProcedureDirectoryRepository

### Community 103 - "Community 103"
Cohesion: 0.12
Nodes (16): net10.0, coverlet.collector, Firely.Fhir.Validation.R4, Hl7.Fhir.R4, Hl7.Fhir.Specification.Data.R4, Microsoft.AspNetCore.OpenApi, Microsoft.IdentityModel.Protocols.OpenIdConnect, Microsoft.NET.Test.Sdk (+8 more)

### Community 104 - "Community 104"
Cohesion: 0.13
Nodes (16): AsyncState, catalogSummary(), CodingCatalogGovernance(), formatDateTime(), Props, statusBadgeClass(), statusLabels, CodingCatalogChangeRequestAction (+8 more)

### Community 105 - "Community 105"
Cohesion: 0.14
Nodes (8): 0e8f4e9 feat(forms): adopt legacy review systems genitourinary, 41fb6b1 feat(forms): display legacy soap snapshots, 8c59573 feat(forms): add clinic note migration manifest, ada3b3e feat(forms): display legacy clinic note snapshots, d681a73 feat(forms): display legacy clinical instructions, LoginResponse, LegacyClinicalFormDisplayEndpoints, clinical_form_migration_manifests

### Community 106 - "Community 106"
Cohesion: 0.28
Nodes (1): ExternalLaboratorySourceRepository

### Community 107 - "Community 107"
Cohesion: 0.27
Nodes (1): InventoryAccountingIntegrationRepository

### Community 110 - "Community 110"
Cohesion: 0.18
Nodes (14): ExperienceAnalyticsEvent, ExperienceBaseline, ExperienceBaselineCounts, ExperienceCriterion, ExperienceEnvironment, ExperienceGap, ExperienceRole, ExperienceTask (+6 more)

### Community 111 - "Community 111"
Cohesion: 0.14
Nodes (10): AppointmentPatient, DURATION_OPTIONS, NewAppointmentDialog(), NewAppointmentDialogProps, PatientSearchState, AppointmentAvailabilityValidationResponse, createAppointment(), getAppointmentSchedulingOptions() (+2 more)

### Community 112 - "Community 112"
Cohesion: 0.13
Nodes (13): _items, Listener, _listeners, notify(), showToast(), ToastContainer(), ToastItem, emptyContactForm (+5 more)

### Community 114 - "Community 114"
Cohesion: 0.14
Nodes (1): ToInventoryLot()

### Community 115 - "Community 115"
Cohesion: 0.14
Nodes (1): ToResponse()

### Community 116 - "Community 116"
Cohesion: 0.25
Nodes (1): TherapyGroupRepository

### Community 117 - "Community 117"
Cohesion: 0.18
Nodes (9): cleanupPracticeSettingGovernanceFixtures(), deleteClinicalListFixture(), deletePatientAdministrationFixtures(), deletePatientDocumentFixtures(), deletePrescriptionFixture(), deleteProcedureOrderFixture(), deleteProviderAssignmentFixtures(), deleteStaffMessageFixture() (+1 more)

### Community 118 - "Community 118"
Cohesion: 0.24
Nodes (16): appointments, capture_patient_portal_appointment_request(), new.appointment_date, new.category_id, new.comments, new.duration_minutes, new.facility_id, new.provider_id (+8 more)

### Community 119 - "Community 119"
Cohesion: 0.18
Nodes (9): 03db92b fix(forms): validate legacy ROS compatibility sections, 193f79b feat(forms): adopt legacy ROS breast pulmonary section, 22c8408 feat(forms): adopt legacy ROS cardiovascular section, 2f3adbd feat(forms): adopt legacy ROS eyes section, 3f1b80a feat(forms): adopt legacy ROS general section, 5545c62 feat(forms): adopt legacy ROS ear nose throat section, 683c9ca feat(forms): adopt legacy ROS gastrointestinal section, 7988299 feat(forms): adopt legacy ROS urinary section (+1 more)

### Community 120 - "Community 120"
Cohesion: 0.16
Nodes (8): c61fd40 fix(recalls): retain closure evidence, e54d1e2 fix(recalls): persist lifecycle events with recalls, RecallConfiguration, RecallLifecycleEventConfiguration, RecallEntity, RecallLifecycleEventEntity, recall_lifecycle_events, recalls

### Community 122 - "Community 122"
Cohesion: 0.32
Nodes (1): EncounterStateRepository

### Community 124 - "Community 124"
Cohesion: 0.25
Nodes (12): ClinicalFormDefinitionSummary, ClinicalFormFieldLocalization, ClinicalFormLocalization, createClinicalFormLocalization(), flattenFields(), flattenRules(), localizeClinicalFormSchema(), localizeClinicalFormSummary() (+4 more)

### Community 125 - "Community 125"
Cohesion: 0.27
Nodes (1): InventoryValuationRepository

### Community 126 - "Community 126"
Cohesion: 0.15
Nodes (5): PhiAuditResourceContextTests, f95ef06 feat(audit): correlate direct PHI access resources, PhiAuditedResult, PhiAuditResourceContext, IResult

### Community 127 - "Community 127"
Cohesion: 0.22
Nodes (14): appointments, auth_accounts, auth_sessions, billing, encounter_signatures, encounters, facilities, immunizations (+6 more)

### Community 128 - "Community 128"
Cohesion: 0.15
Nodes (11): initialDefinition, InventoryCostPolicyGovernancePanel(), labelForMethod(), Props, createInventoryCostPolicyChangeRequest(), getInventoryCostPolicies(), getInventoryCostPolicyChangeRequest(), InventoryCostPolicyChangeRequest (+3 more)

### Community 129 - "Community 129"
Cohesion: 0.14
Nodes (5): 10b94e5 feat(forms): adopt legacy ankle assessment, 21a1e03 feat(forms): adopt legacy prior authorization, 9d6a4b8 feat(forms): adopt legacy treatment plan, d0ab407 feat(forms): adopt legacy work school note, da2d960 feat(forms): adopt legacy physical exam lines

### Community 130 - "Community 130"
Cohesion: 0.16
Nodes (6): 1764980 feat(encounters): show locked track state, PatientReadingAccumulator, Get-EncounterDetail(), Get-HttpStatus(), Invoke-JsonRequest(), Invoke-StatusRequest()

### Community 133 - "Community 133"
Cohesion: 0.35
Nodes (1): PatientSdohRepository

### Community 134 - "Community 134"
Cohesion: 0.36
Nodes (1): AvenChartOpenApi

### Community 135 - "Community 135"
Cohesion: 0.22
Nodes (13): allergies, avenchart_require_active_patient_for_new_clinical_content(), immunizations, medications, patient_record, patients, prescriptions, problems (+5 more)

### Community 136 - "Community 136"
Cohesion: 0.14
Nodes (12): areaTotals, commits, here, historyBasePath, historyRef, log, monthly, outputPath (+4 more)

### Community 137 - "Community 137"
Cohesion: 0.29
Nodes (11): external_laboratory_ingestion_events, external_laboratory_ingestions, external_laboratory_report_links, external_laboratory_result_links, external_laboratory_sources, lab_orders, lab_reports, lab_results (+3 more)

### Community 138 - "Community 138"
Cohesion: 0.28
Nodes (12): an, avenchart_capture_lab_report_review_content(), lab_report_review_events, lab_reports, lab_results, old.content_checksum, old.content_manifest, old.content_revision (+4 more)

### Community 139 - "Community 139"
Cohesion: 0.21
Nodes (4): RuntimeSafetyPolicyTests, 7be4153 feat(runtime): fail closed for production hosting, 7f7a66f fix(billing): isolate generated financial fixtures, RuntimeSafetyOptions

### Community 140 - "Community 140"
Cohesion: 0.20
Nodes (6): AsyncState, PatientMessages(), statusClass(), getPatientMessages(), PatientMessageItem, PatientMessagesResponse

### Community 141 - "Community 141"
Cohesion: 0.24
Nodes (1): AzureOperationsAccessRepository

### Community 142 - "Community 142"
Cohesion: 0.33
Nodes (1): BatchCommunicationRepository

### Community 143 - "Community 143"
Cohesion: 0.38
Nodes (1): LegacyClinicalFormDisplayRepository

### Community 144 - "Community 144"
Cohesion: 0.30
Nodes (1): EndpointAccessPolicies

### Community 145 - "Community 145"
Cohesion: 0.24
Nodes (5): IPatientPortalIdentityAdapter, LocalPatientPortalIdentityAdapter, OidcPatientPortalIdentityAdapter, PatientPortalIdentityAdapterHelpers, TestOidcPatientPortalIdentityAdapter

### Community 146 - "Community 146"
Cohesion: 0.18
Nodes (4): 18dd71c feat(forms): adopt legacy speech dictation, 3fbf1fd feat(forms): adopt legacy phq9 screening, 7ad81c7 feat(forms): adopt legacy gad7 screening, aebec7a feat(forms): adopt legacy transfer summary

### Community 147 - "Community 147"
Cohesion: 0.44
Nodes (1): ClinicalAlertEvaluationRepository

### Community 148 - "Community 148"
Cohesion: 0.36
Nodes (1): EncounterLayoutFormRepository

### Community 149 - "Community 149"
Cohesion: 0.40
Nodes (1): ExternalIdentityMappingRepository

### Community 150 - "Community 150"
Cohesion: 0.40
Nodes (1): PatientPortalExternalIdentityMappingRepository

### Community 151 - "Community 151"
Cohesion: 0.51
Nodes (1): PatientPrintRepository

### Community 152 - "Community 152"
Cohesion: 0.44
Nodes (1): PatientXmlExchangeRepository

### Community 153 - "Community 153"
Cohesion: 0.35
Nodes (10): avenchart_reject_locked_encounter_mutation(), encounter_signatures, encounter_track_records, encounters, is_locked, lab_orders, lab_reports, or (+2 more)

### Community 154 - "Community 154"
Cohesion: 0.31
Nodes (1): FhirR4ValidationServiceTests

### Community 155 - "Community 155"
Cohesion: 0.22
Nodes (8): AsyncState, AuthorizationPolicyRegistry(), formatGap(), gapOptions, AuthorizationPolicyCatalogResponse, AuthorizationPolicyGap, AuthorizationPolicyRule, getAuthorizationPolicyCatalog()

### Community 156 - "Community 156"
Cohesion: 0.27
Nodes (4): FlowBoardItem, FlowBoardResponse, getAppointmentFlowBoard(), updateAppointmentStatus()

### Community 157 - "Community 157"
Cohesion: 0.22
Nodes (3): 6b68b32 perf(patients): govern facility search plan, a69926c ci: pin workflow action revisions, bfbd7c6 perf(schedule): index operational flow board

### Community 158 - "Community 158"
Cohesion: 0.31
Nodes (8): allowedTransitions, APPOINTMENT_STATUSES, AppointmentSemanticStatus, AppointmentStatusDefinition, getAppointmentStatus(), getAppointmentStatusOptions(), isCancelledAppointment(), unknownStatus

### Community 159 - "Community 159"
Cohesion: 0.38
Nodes (1): DatabaseSchemaMigrator

### Community 160 - "Community 160"
Cohesion: 0.33
Nodes (4): IStaffIdentityAdapter, OidcIdentityAdapterHelpers, OidcStaffIdentityAdapter, TestOidcStaffIdentityAdapter

### Community 161 - "Community 161"
Cohesion: 0.33
Nodes (9): allergies, avenchart_advance_allergy_review_state(), avenchart_initialize_allergy_review_state(), can, old.pid, patient_allergy_review_states, patients, trg_allergies_advance_review_state (+1 more)

### Community 162 - "Community 162"
Cohesion: 0.20
Nodes (10): Archive-DocumentTestFixture(), Archive-EncounterTestFixture(), Archive-MessageTestFixture(), Cancel-AppointmentTestFixture(), Get-AdministrationHeaders(), New-AuthenticatedHttpClient(), New-ReceivedProcedureSpecimen(), Set-AdministrationFacilityContext() (+2 more)

### Community 163 - "Community 163"
Cohesion: 0.36
Nodes (1): AddressBookRepository

### Community 166 - "Community 166"
Cohesion: 0.47
Nodes (8): facilities, inventory_cost_layer_applications, inventory_cost_layers, inventory_cost_policies, inventory_items, inventory_lots, inventory_purchase_receipts, inventory_transactions

### Community 167 - "Community 167"
Cohesion: 0.50
Nodes (8): patients, staff, therapy_group_members, therapy_group_session_attendance, therapy_group_session_encounters, therapy_group_session_participants, therapy_group_sessions, therapy_groups

### Community 168 - "Community 168"
Cohesion: 0.25
Nodes (8): critical_lab_result_acknowledgement_events, critical_lab_result_acknowledgements, lab_orders, lab_report_review_events, lab_reports, lab_results, lab_specimens, procedure_result_versions

### Community 169 - "Community 169"
Cohesion: 0.29
Nodes (1): RuntimeSafetyPolicy

### Community 170 - "Community 170"
Cohesion: 0.43
Nodes (1): OfficeNoteRepository

### Community 172 - "Community 172"
Cohesion: 0.39
Nodes (1): RecallRepository

### Community 173 - "Community 173"
Cohesion: 0.50
Nodes (7): facilities, inventory_cost_layers, inventory_cost_policies, inventory_items, inventory_lots, inventory_valuation_run_lines, inventory_valuation_runs

### Community 174 - "Community 174"
Cohesion: 0.57
Nodes (7): clinical_form_definition_events, clinical_form_definitions, clinical_form_instance_events, clinical_form_instances, clinical_form_revisions, clinical_form_signatures, patients

### Community 175 - "Community 175"
Cohesion: 0.25
Nodes (7): assetsRoot, distRoot, files, initial, initialMatch, result, violations

### Community 176 - "Community 176"
Cohesion: 0.46
Nodes (1): ClinicalWorkflowPolicyCatalog

### Community 177 - "Community 177"
Cohesion: 0.57
Nodes (1): DatabaseBootstrapCatalogTests

### Community 178 - "Community 178"
Cohesion: 0.48
Nodes (1): ChartTrackerRepository

### Community 180 - "Community 180"
Cohesion: 0.29
Nodes (3): IHealthCheck, PostgresReadinessHealthCheck, SchemaMigrationReadinessHealthCheck

### Community 181 - "Community 181"
Cohesion: 0.52
Nodes (6): facilities, inventory_items, inventory_purchase_requisition_events, inventory_purchase_requisition_lines, inventory_purchase_requisitions, inventory_vendors

### Community 182 - "Community 182"
Cohesion: 0.57
Nodes (6): inventory_controlled_count_discrepancies, inventory_controlled_count_lines, inventory_controlled_count_sessions, inventory_controlled_custody_events, inventory_controlled_locations, inventory_lots

### Community 183 - "Community 183"
Cohesion: 0.62
Nodes (6): facilities, inventory_items, inventory_replenishment_policies, inventory_replenishment_policy_change_request_events, inventory_replenishment_policy_change_requests, inventory_vendors

### Community 184 - "Community 184"
Cohesion: 0.48
Nodes (5): external_laboratory_source_facility_events, external_laboratory_source_facility_grants, external_laboratory_sources, facilities, trg_external_laboratory_source_facility_events_immutable

### Community 185 - "Community 185"
Cohesion: 0.33
Nodes (2): Get-PathOperation(), Get-PropertyValue()

### Community 186 - "Community 186"
Cohesion: 0.67
Nodes (1): PatientRecordRequestRepository

### Community 187 - "Community 187"
Cohesion: 0.67
Nodes (1): AzureOperationsEndpoints

### Community 188 - "Community 188"
Cohesion: 0.67
Nodes (5): encounter_track_reading_values, encounter_track_readings, encounter_track_records, encounters, track_anything_types

### Community 189 - "Community 189"
Cohesion: 0.60
Nodes (5): encounters, inventory_lots, inventory_patient_sales, inventory_transactions, patients

### Community 190 - "Community 190"
Cohesion: 0.53
Nodes (5): facilities, inventory_controlled_item_classification_events, inventory_controlled_location_events, inventory_controlled_locations, inventory_items

### Community 191 - "Community 191"
Cohesion: 0.67
Nodes (5): encounters, inventory_controlled_custody_events, inventory_controlled_locations, inventory_lots, patients

### Community 192 - "Community 192"
Cohesion: 0.67
Nodes (5): patient_disclosure_authorities, patient_disclosure_authority_events, patient_disclosure_request_events, patient_disclosure_requests, patients

### Community 193 - "Community 193"
Cohesion: 0.60
Nodes (5): auth_accounts, facilities, practice_setting_delegation_events, practice_setting_delegations, practice_settings

### Community 194 - "Community 194"
Cohesion: 0.33
Nodes (5): operations.audit_events, operations.operator_credentials, operations.runtime_state, operations.sessions, operations.usage_events

### Community 195 - "Community 195"
Cohesion: 0.47
Nodes (5): auth_sessions, azure_operations_access_audit, azure_operations_access_config, azure_operations_access_grants, azure_operations_unlock_attempts

### Community 196 - "Community 196"
Cohesion: 0.67
Nodes (5): auth_access_context_grant_events, auth_accounts, auth_principal_facility_grants, auth_principal_purpose_of_use_grants, facilities

### Community 197 - "Community 197"
Cohesion: 0.47
Nodes (4): prescription_audit_events, prescriptions, trg_prescription_audit_events_immutable, trg_prescriptions_retained

### Community 198 - "Community 198"
Cohesion: 0.53
Nodes (5): avenchart_require_active_patient_for_prescription_continuation(), patient_record, patients, prescriptions, trg_prescriptions_require_active_patient_for_continuation

### Community 199 - "Community 199"
Cohesion: 0.53
Nodes (4): auth_accounts, auth_external_identity_mapping_events, auth_external_identity_mappings, trg_auth_external_identity_mapping_events_immutable

### Community 200 - "Community 200"
Cohesion: 0.53
Nodes (4): patient_portal_external_identity_mapping_events, patient_portal_external_identity_mappings, patients, trg_patient_portal_external_identity_mapping_events_immutable

### Community 201 - "Community 201"
Cohesion: 0.53
Nodes (4): critical_lab_result_follow_up_events, critical_lab_result_follow_ups, lab_results, trg_critical_follow_up_events_append_only

### Community 202 - "Community 202"
Cohesion: 0.40
Nodes (2): Invoke-Api(), Start-TestApi()

### Community 203 - "Community 203"
Cohesion: 0.40
Nodes (2): Get-EncounterDetail(), Invoke-JsonRequest()

### Community 204 - "Community 204"
Cohesion: 0.40
Nodes (2): Assert-GeneratedAppointmentId(), New-PortalRequest()

### Community 205 - "Community 205"
Cohesion: 0.53
Nodes (1): TestIdentityProviderService

### Community 206 - "Community 206"
Cohesion: 0.40
Nodes (5): inventory_items, inventory_lots, inventory_purchase_receipts, inventory_transactions, inventory_vendors

### Community 207 - "Community 207"
Cohesion: 0.70
Nodes (1): FlowBoardRepository

### Community 208 - "Community 208"
Cohesion: 0.60
Nodes (1): PhiAuditRepository

### Community 209 - "Community 209"
Cohesion: 0.60
Nodes (1): DevelopmentTestIdentityProviderEndpoints

### Community 210 - "Community 210"
Cohesion: 0.60
Nodes (1): FhirR4Endpoints

### Community 211 - "Community 211"
Cohesion: 0.80
Nodes (1): FhirR4ValidationService

### Community 212 - "Community 212"
Cohesion: 0.60
Nodes (1): PatientEndpoints

### Community 213 - "Community 213"
Cohesion: 0.50
Nodes (1): SchemaMigrationCatalog

### Community 214 - "Community 214"
Cohesion: 0.70
Nodes (4): facilities, inventory_items, inventory_lots, inventory_transactions

### Community 215 - "Community 215"
Cohesion: 0.70
Nodes (4): patient_merge_audit_plans, patient_merge_execution_manifest_rows, patient_merge_executions, patients

### Community 216 - "Community 216"
Cohesion: 0.70
Nodes (4): facilities, patients, recalls, staff

### Community 217 - "Community 217"
Cohesion: 0.70
Nodes (4): chart_tracker_events, chart_tracker_locations, patients, staff

### Community 218 - "Community 218"
Cohesion: 0.70
Nodes (4): encounters, inventory_items, inventory_patient_sale_batches, patients

### Community 219 - "Community 219"
Cohesion: 0.80
Nodes (4): inventory_item_medication_link_audits, inventory_item_medication_links, inventory_items, medication_vocabulary

### Community 220 - "Community 220"
Cohesion: 0.70
Nodes (4): inventory_purchase_receipts, inventory_purchase_requisition_lines, inventory_purchase_requisition_receipts, inventory_purchase_requisitions

### Community 221 - "Community 221"
Cohesion: 0.70
Nodes (4): inventory_lot_destructions, inventory_lot_expiry_dispositions, inventory_lots, inventory_transactions

### Community 222 - "Community 222"
Cohesion: 0.70
Nodes (4): authorizations, clinical_workflow_events, patients, referrals

### Community 223 - "Community 223"
Cohesion: 0.90
Nodes (4): saved_report_definition_events, saved_report_definition_revisions, saved_report_definitions, saved_report_runs

### Community 224 - "Community 224"
Cohesion: 0.70
Nodes (4): azure_deployment_execution_events, azure_deployment_executions, azure_deployment_profile_revisions, azure_deployment_profiles

### Community 225 - "Community 225"
Cohesion: 0.60
Nodes (3): clinical_list_audit_events, patients, trg_clinical_list_audit_events_immutable

### Community 226 - "Community 226"
Cohesion: 0.60
Nodes (3): external_laboratory_source_events, external_laboratory_sources, trg_external_laboratory_source_events_immutable

### Community 227 - "Community 227"
Cohesion: 0.60
Nodes (4): lab_orders, lab_results, procedure_order_events, procedure_result_events

### Community 228 - "Community 228"
Cohesion: 0.60
Nodes (3): integration_outbox, integration_outbox_provenance_events, trg_integration_outbox_provenance_events_immutable

### Community 230 - "Community 230"
Cohesion: 0.50
Nodes (2): Invoke-FixtureSql(), Set-FixturePortalState()

### Community 233 - "Community 233"
Cohesion: 0.50
Nodes (1): AuthorizationPolicyCatalog

### Community 234 - "Community 234"
Cohesion: 0.50
Nodes (1): StaffAccessContextServiceTests

### Community 235 - "Community 235"
Cohesion: 0.67
Nodes (4): access_group_permissions, access_groups, access_permissions, access_user_memberships

### Community 236 - "Community 236"
Cohesion: 0.50
Nodes (4): patient_disclosure_authorities, patient_disclosure_authority_events, patient_disclosure_request_events, patient_disclosure_requests

### Community 237 - "Community 237"
Cohesion: 0.83
Nodes (1): PatientMergeAuditRepository

### Community 238 - "Community 238"
Cohesion: 0.67
Nodes (1): ExternalLaboratoryFhirIntakeEndpoints

### Community 239 - "Community 239"
Cohesion: 0.67
Nodes (1): IntegrationEndpoints

### Community 240 - "Community 240"
Cohesion: 0.83
Nodes (3): form_layout_fields, form_layout_groups, form_layouts

### Community 241 - "Community 241"
Cohesion: 0.83
Nodes (3): encounter_layout_form_records, encounter_layout_form_values, form_layouts

### Community 242 - "Community 242"
Cohesion: 0.83
Nodes (3): batch_communication_campaigns, batch_communication_recipients, patients

### Community 243 - "Community 243"
Cohesion: 0.83
Nodes (3): facilities, inventory_purchase_receipts, inventory_vendors

### Community 244 - "Community 244"
Cohesion: 0.83
Nodes (3): practice_setting_change_request_events, practice_setting_change_requests, practice_settings

### Community 245 - "Community 245"
Cohesion: 0.83
Nodes (3): document_template_binary_versions, document_template_events, document_templates

### Community 246 - "Community 246"
Cohesion: 0.83
Nodes (3): inventory_cost_policies, inventory_cost_policy_change_request_events, inventory_cost_policy_change_requests

### Community 247 - "Community 247"
Cohesion: 0.83
Nodes (3): inventory_accounting_integration_change_request_events, inventory_accounting_integration_change_requests, inventory_accounting_integration_decisions

### Community 248 - "Community 248"
Cohesion: 0.83
Nodes (3): facilities, practice_setting_facility_overrides, practice_settings

### Community 249 - "Community 249"
Cohesion: 0.83
Nodes (3): practice_setting_change_requests, practice_setting_facility_override_revisions, practice_setting_facility_overrides

### Community 250 - "Community 250"
Cohesion: 0.83
Nodes (3): encounters, legacy_clinical_form_snapshots, patients

### Community 251 - "Community 251"
Cohesion: 0.83
Nodes (3): message_assignment_events, messages, patients

### Community 252 - "Community 252"
Cohesion: 0.83
Nodes (3): messages, patients, staff_message_attachments

### Community 253 - "Community 253"
Cohesion: 0.83
Nodes (3): message_correction_events, messages, patients

### Community 254 - "Community 254"
Cohesion: 0.83
Nodes (3): message_retention_events, messages, patients

### Community 255 - "Community 255"
Cohesion: 0.83
Nodes (3): critical_lab_result_acknowledgement_events, critical_lab_result_acknowledgements, lab_results

### Community 256 - "Community 256"
Cohesion: 0.83
Nodes (3): message_content_events, messages, patients

### Community 257 - "Community 257"
Cohesion: 0.83
Nodes (3): integration_idempotency_conflicts, integration_inbox, integration_outbox

### Community 258 - "Community 258"
Cohesion: 0.67
Nodes (1): AdministrationEndpoints

### Community 259 - "Community 259"
Cohesion: 0.67
Nodes (1): AdministrativeReferenceEndpoints

### Community 260 - "Community 260"
Cohesion: 0.67
Nodes (1): AppointmentEndpoints

### Community 261 - "Community 261"
Cohesion: 0.67
Nodes (1): BillingEndpoints

### Community 262 - "Community 262"
Cohesion: 0.67
Nodes (1): ClinicalFormEndpoints

### Community 263 - "Community 263"
Cohesion: 0.67
Nodes (1): ClinicalListEndpoints

### Community 264 - "Community 264"
Cohesion: 0.67
Nodes (1): ClinicalWorkflowEndpoints

### Community 265 - "Community 265"
Cohesion: 0.67
Nodes (1): ConfigurationEndpoints

### Community 266 - "Community 266"
Cohesion: 0.67
Nodes (1): DocumentEndpoints

### Community 267 - "Community 267"
Cohesion: 0.67
Nodes (1): DocumentTemplateEndpoints

### Community 268 - "Community 268"
Cohesion: 0.67
Nodes (1): EncounterEndpoints

### Community 269 - "Community 269"
Cohesion: 0.67
Nodes (1): InventoryEndpoints

### Community 270 - "Community 270"
Cohesion: 0.67
Nodes (1): ManagedRecordEndpoints

### Community 271 - "Community 271"
Cohesion: 0.67
Nodes (1): MessageEndpoints

### Community 272 - "Community 272"
Cohesion: 0.67
Nodes (1): OfficeNoteEndpoints

### Community 273 - "Community 273"
Cohesion: 0.67
Nodes (1): PatientEngagementEndpoints

### Community 274 - "Community 274"
Cohesion: 0.67
Nodes (1): PatientPortalEndpoints

### Community 275 - "Community 275"
Cohesion: 0.67
Nodes (1): ProcedureEndpoints

### Community 276 - "Community 276"
Cohesion: 0.67
Nodes (1): ReportEndpoints

### Community 277 - "Community 277"
Cohesion: 0.67
Nodes (1): StaffAuthenticationEndpoints

### Community 278 - "Community 278"
Cohesion: 0.67
Nodes (1): TherapyGroupEndpoints

### Community 279 - "Community 279"
Cohesion: 0.67
Nodes (2): integration_inbox, integration_outbox

### Community 280 - "Community 280"
Cohesion: 0.67
Nodes (2): practice_setting_audit_events, practice_settings

### Community 281 - "Community 281"
Cohesion: 1.00
Nodes (2): coding_catalog_audit_events, coding_catalogs

### Community 282 - "Community 282"
Cohesion: 1.00
Nodes (2): form_option_lists, form_option_values

### Community 283 - "Community 283"
Cohesion: 1.00
Nodes (2): clinical_alert_rules, encounter_clinical_alert_acknowledgments

### Community 284 - "Community 284"
Cohesion: 1.00
Nodes (2): patient_record_requests, patients

### Community 285 - "Community 285"
Cohesion: 1.00
Nodes (2): patient_sdoh_assessments, patients

### Community 286 - "Community 286"
Cohesion: 1.00
Nodes (2): recall_activity, recalls

### Community 287 - "Community 287"
Cohesion: 1.00
Nodes (2): patient_duplicate_review_dispositions, patients

### Community 288 - "Community 288"
Cohesion: 1.00
Nodes (2): document_template_binary_versions, document_templates

### Community 289 - "Community 289"
Cohesion: 1.00
Nodes (2): patient_xml_exchange_audits, patients

### Community 290 - "Community 290"
Cohesion: 1.00
Nodes (2): inventory_count_reconciliations, inventory_lots

### Community 291 - "Community 291"
Cohesion: 1.33
Nodes (2): practice_setting_revisions, practice_settings

### Community 292 - "Community 292"
Cohesion: 1.33
Nodes (2): coding_catalog_revisions, coding_catalogs

### Community 293 - "Community 293"
Cohesion: 1.33
Nodes (2): form_option_list_revisions, form_option_lists

### Community 294 - "Community 294"
Cohesion: 1.33
Nodes (2): form_layout_revisions, form_layouts

### Community 295 - "Community 295"
Cohesion: 1.33
Nodes (2): clinical_alert_rule_revisions, clinical_alert_rules

### Community 296 - "Community 296"
Cohesion: 1.33
Nodes (2): module_catalog, module_catalog_revisions

### Community 297 - "Community 297"
Cohesion: 1.33
Nodes (2): api_client_registry, api_client_registry_revisions

### Community 298 - "Community 298"
Cohesion: 1.00
Nodes (2): inventory_lot_metadata_audits, inventory_lots

### Community 299 - "Community 299"
Cohesion: 1.00
Nodes (2): inventory_lot_destructions, inventory_lots

### Community 300 - "Community 300"
Cohesion: 1.00
Nodes (2): inventory_controlled_locations, inventory_controlled_report_runs

### Community 301 - "Community 301"
Cohesion: 1.00
Nodes (2): inventory_controlled_report_exports, inventory_controlled_report_runs

### Community 302 - "Community 302"
Cohesion: 1.00
Nodes (2): coding_catalog_change_request_events, coding_catalog_change_requests

### Community 303 - "Community 303"
Cohesion: 1.00
Nodes (2): form_layout_change_request_events, form_layout_change_requests

### Community 304 - "Community 304"
Cohesion: 1.00
Nodes (2): form_option_list_change_request_events, form_option_list_change_requests

### Community 305 - "Community 305"
Cohesion: 1.00
Nodes (2): clinical_alert_rule_change_request_events, clinical_alert_rule_change_requests

### Community 306 - "Community 306"
Cohesion: 1.00
Nodes (2): module_change_request_events, module_change_requests

### Community 307 - "Community 307"
Cohesion: 1.00
Nodes (2): api_client_change_request_events, api_client_change_requests

### Community 308 - "Community 308"
Cohesion: 1.00
Nodes (2): configuration_package_import_request_events, configuration_package_import_requests

### Community 309 - "Community 309"
Cohesion: 1.00
Nodes (2): clinical_form_migration_manifest_events, clinical_form_migration_manifests

### Community 310 - "Community 310"
Cohesion: 1.00
Nodes (2): message_escalation_events, messages

### Community 311 - "Community 311"
Cohesion: 1.00
Nodes (2): lab_specimens, procedure_specimen_events

### Community 312 - "Community 312"
Cohesion: 1.00
Nodes (2): patient_registration_duplicate_reviews, patients

### Community 313 - "Community 313"
Cohesion: 1.00
Nodes (2): pharmacies, prescriptions

### Community 314 - "Community 314"
Cohesion: 1.00
Nodes (1): DatabaseConnectionOptions

### Community 315 - "Community 315"
Cohesion: 1.00
Nodes (1): schema_migrations

### Community 316 - "Community 316"
Cohesion: 1.00
Nodes (1): statement_email_outbox

### Community 317 - "Community 317"
Cohesion: 1.00
Nodes (1): phi_access_audit_events

### Community 318 - "Community 318"
Cohesion: 1.00
Nodes (1): encounter_audit_events

### Community 319 - "Community 319"
Cohesion: 1.00
Nodes (1): clinical_alert_rules

### Community 320 - "Community 320"
Cohesion: 1.00
Nodes (1): module_catalog

### Community 321 - "Community 321"
Cohesion: 1.00
Nodes (1): api_client_registry

### Community 322 - "Community 322"
Cohesion: 1.00
Nodes (1): office_notes

### Community 323 - "Community 323"
Cohesion: 1.00
Nodes (1): address_book_contacts

### Community 324 - "Community 324"
Cohesion: 2.00
Nodes (1): track_anything_types

### Community 325 - "Community 325"
Cohesion: 1.00
Nodes (1): patient_education_resources

### Community 326 - "Community 326"
Cohesion: 1.00
Nodes (1): document_templates

### Community 327 - "Community 327"
Cohesion: 1.00
Nodes (1): inventory_controlled_action_attestations

### Community 328 - "Community 328"
Cohesion: 1.00
Nodes (1): AvenChart.Api.csproj

## Knowledge Gaps
- **836 isolated node(s):** `AccessibilityFinding`, `clinicianFixture`, `codingEncounter`, `encounter`, `composeRoot` (+831 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Community 7`** (1 nodes): `BillingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 8`** (1 nodes): `DocumentRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 12`** (1 nodes): `PatientRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 13`** (1 nodes): `AppointmentRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 14`** (1 nodes): `ProcedureRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 15`** (1 nodes): `af0f321 fix(validation): harden runtime workflow evidence`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 23`** (2 nodes): `DiagnosisAccumulator`, `EncounterRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 26`** (1 nodes): `PatientPortalRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 27`** (1 nodes): `ClinicalFormRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 30`** (1 nodes): `AdministrationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 31`** (1 nodes): `ClinicalFormRuntime`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 36`** (1 nodes): `ReportExecutionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 37`** (1 nodes): `ClinicalListRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 42`** (1 nodes): `MessageRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 47`** (2 nodes): `InventoryItemBuilder`, `InventoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 50`** (1 nodes): `ManagedRecordRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 52`** (1 nodes): `ReportRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 54`** (1 nodes): `BrowserOidcSessionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 55`** (1 nodes): `ReportDefinitionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 65`** (1 nodes): `AuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 66`** (1 nodes): `IntegrationIdempotencyConflictException`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 67`** (1 nodes): `PatientDisclosureRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 70`** (1 nodes): `AzureOperationsRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 71`** (1 nodes): `ClinicalListStateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 72`** (1 nodes): `FhirRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 74`** (1 nodes): `AzureDeploymentProfilePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 79`** (1 nodes): `ReferralRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 82`** (1 nodes): `DocumentTemplateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 83`** (1 nodes): `PatientMergeExecutionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 84`** (2 nodes): `ReportExecutionQueueRepository`, `WorkerCancellationState`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 88`** (1 nodes): `InventoryReplenishmentPolicyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 97`** (1 nodes): `AdministrationDirectoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 99`** (2 nodes): `AuthRepository`, `ToResponse()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 100`** (1 nodes): `InventoryCostPolicyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 102`** (1 nodes): `ProcedureDirectoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 106`** (1 nodes): `ExternalLaboratorySourceRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 107`** (1 nodes): `InventoryAccountingIntegrationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 114`** (1 nodes): `ToInventoryLot()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 115`** (1 nodes): `ToResponse()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 116`** (1 nodes): `TherapyGroupRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 122`** (1 nodes): `EncounterStateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 125`** (1 nodes): `InventoryValuationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 133`** (1 nodes): `PatientSdohRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 134`** (1 nodes): `AvenChartOpenApi`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 141`** (1 nodes): `AzureOperationsAccessRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 142`** (1 nodes): `BatchCommunicationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 143`** (1 nodes): `LegacyClinicalFormDisplayRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 144`** (1 nodes): `EndpointAccessPolicies`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 147`** (1 nodes): `ClinicalAlertEvaluationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 148`** (1 nodes): `EncounterLayoutFormRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 149`** (1 nodes): `ExternalIdentityMappingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 150`** (1 nodes): `PatientPortalExternalIdentityMappingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 151`** (1 nodes): `PatientPrintRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 152`** (1 nodes): `PatientXmlExchangeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 154`** (1 nodes): `FhirR4ValidationServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 159`** (1 nodes): `DatabaseSchemaMigrator`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 163`** (1 nodes): `AddressBookRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 169`** (1 nodes): `RuntimeSafetyPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 170`** (1 nodes): `OfficeNoteRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 172`** (1 nodes): `RecallRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 176`** (1 nodes): `ClinicalWorkflowPolicyCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 177`** (1 nodes): `DatabaseBootstrapCatalogTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 178`** (1 nodes): `ChartTrackerRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 185`** (2 nodes): `Get-PathOperation()`, `Get-PropertyValue()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 186`** (1 nodes): `PatientRecordRequestRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 187`** (1 nodes): `AzureOperationsEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 202`** (2 nodes): `Invoke-Api()`, `Start-TestApi()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 203`** (2 nodes): `Get-EncounterDetail()`, `Invoke-JsonRequest()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 204`** (2 nodes): `Assert-GeneratedAppointmentId()`, `New-PortalRequest()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 205`** (1 nodes): `TestIdentityProviderService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 207`** (1 nodes): `FlowBoardRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 208`** (1 nodes): `PhiAuditRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 209`** (1 nodes): `DevelopmentTestIdentityProviderEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 210`** (1 nodes): `FhirR4Endpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 211`** (1 nodes): `FhirR4ValidationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 212`** (1 nodes): `PatientEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 213`** (1 nodes): `SchemaMigrationCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 230`** (2 nodes): `Invoke-FixtureSql()`, `Set-FixturePortalState()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 233`** (1 nodes): `AuthorizationPolicyCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 234`** (1 nodes): `StaffAccessContextServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 237`** (1 nodes): `PatientMergeAuditRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 238`** (1 nodes): `ExternalLaboratoryFhirIntakeEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 239`** (1 nodes): `IntegrationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 258`** (1 nodes): `AdministrationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 259`** (1 nodes): `AdministrativeReferenceEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 260`** (1 nodes): `AppointmentEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 261`** (1 nodes): `BillingEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 262`** (1 nodes): `ClinicalFormEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 263`** (1 nodes): `ClinicalListEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 264`** (1 nodes): `ClinicalWorkflowEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 265`** (1 nodes): `ConfigurationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 266`** (1 nodes): `DocumentEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 267`** (1 nodes): `DocumentTemplateEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 268`** (1 nodes): `EncounterEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 269`** (1 nodes): `InventoryEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 270`** (1 nodes): `ManagedRecordEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 271`** (1 nodes): `MessageEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 272`** (1 nodes): `OfficeNoteEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 273`** (1 nodes): `PatientEngagementEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 274`** (1 nodes): `PatientPortalEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 275`** (1 nodes): `ProcedureEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 276`** (1 nodes): `ReportEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 277`** (1 nodes): `StaffAuthenticationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 278`** (1 nodes): `TherapyGroupEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 279`** (2 nodes): `integration_inbox`, `integration_outbox`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 280`** (2 nodes): `practice_setting_audit_events`, `practice_settings`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 281`** (2 nodes): `coding_catalog_audit_events`, `coding_catalogs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 282`** (2 nodes): `form_option_lists`, `form_option_values`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 283`** (2 nodes): `clinical_alert_rules`, `encounter_clinical_alert_acknowledgments`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 284`** (2 nodes): `patient_record_requests`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 285`** (2 nodes): `patient_sdoh_assessments`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 286`** (2 nodes): `recall_activity`, `recalls`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 287`** (2 nodes): `patient_duplicate_review_dispositions`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 288`** (2 nodes): `document_template_binary_versions`, `document_templates`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 289`** (2 nodes): `patient_xml_exchange_audits`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 290`** (2 nodes): `inventory_count_reconciliations`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 291`** (2 nodes): `practice_setting_revisions`, `practice_settings`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 292`** (2 nodes): `coding_catalog_revisions`, `coding_catalogs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 293`** (2 nodes): `form_option_list_revisions`, `form_option_lists`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 294`** (2 nodes): `form_layout_revisions`, `form_layouts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 295`** (2 nodes): `clinical_alert_rule_revisions`, `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 296`** (2 nodes): `module_catalog`, `module_catalog_revisions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 297`** (2 nodes): `api_client_registry`, `api_client_registry_revisions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 298`** (2 nodes): `inventory_lot_metadata_audits`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 299`** (2 nodes): `inventory_lot_destructions`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 300`** (2 nodes): `inventory_controlled_locations`, `inventory_controlled_report_runs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 301`** (2 nodes): `inventory_controlled_report_exports`, `inventory_controlled_report_runs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 302`** (2 nodes): `coding_catalog_change_request_events`, `coding_catalog_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 303`** (2 nodes): `form_layout_change_request_events`, `form_layout_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 304`** (2 nodes): `form_option_list_change_request_events`, `form_option_list_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 305`** (2 nodes): `clinical_alert_rule_change_request_events`, `clinical_alert_rule_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 306`** (2 nodes): `module_change_request_events`, `module_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 307`** (2 nodes): `api_client_change_request_events`, `api_client_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 308`** (2 nodes): `configuration_package_import_request_events`, `configuration_package_import_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 309`** (2 nodes): `clinical_form_migration_manifest_events`, `clinical_form_migration_manifests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 310`** (2 nodes): `message_escalation_events`, `messages`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 311`** (2 nodes): `lab_specimens`, `procedure_specimen_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 312`** (2 nodes): `patient_registration_duplicate_reviews`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 313`** (2 nodes): `pharmacies`, `prescriptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 314`** (1 nodes): `DatabaseConnectionOptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 315`** (1 nodes): `schema_migrations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 316`** (1 nodes): `statement_email_outbox`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 317`** (1 nodes): `phi_access_audit_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 318`** (1 nodes): `encounter_audit_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 319`** (1 nodes): `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 320`** (1 nodes): `module_catalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 321`** (1 nodes): `api_client_registry`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 322`** (1 nodes): `office_notes`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 323`** (1 nodes): `address_book_contacts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 324`** (1 nodes): `track_anything_types`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 325`** (1 nodes): `patient_education_resources`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 326`** (1 nodes): `document_templates`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 327`** (1 nodes): `inventory_controlled_action_attestations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 328`** (1 nodes): `AvenChart.Api.csproj`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AdministrationRepository` connect `Community 30` to `Community 6`, `Community 113`, `Community 121`, `Community 90`, `Community 78`, `Community 98`, `Community 131`, `Community 69`, `Community 81`, `Community 87`?**
  _High betweenness centrality (0.065) - this node is a cross-community bridge._
- **Why does `PatientPortalRepository` connect `Community 26` to `Community 21`, `Community 108`, `Community 109`, `Community 123`, `Community 60`, `Community 53`, `Community 164`, `Community 165`, `Community 115`, `Community 171`?**
  _High betweenness centrality (0.060) - this node is a cross-community bridge._
- **Why does `InventoryRepository` connect `Community 47` to `Community 96`, `Community 179`, `Community 101`, `Community 91`, `Community 132`, `Community 114`?**
  _High betweenness centrality (0.031) - this node is a cross-community bridge._
- **What connects `AccessibilityFinding`, `clinicianFixture`, `codingEncounter` to the rest of the system?**
  _836 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.006881758870319063 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.011169024571854059 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.046085011185682326 - nodes in this community are weakly interconnected._