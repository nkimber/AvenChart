# Graph Report - .  (2026-08-30)

## Corpus Check
- Large corpus: 1113 files · ~1,104,750 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder, or use --no-semantic to run AST-only.

## Summary
- 10432 nodes · 23370 edges · 544 communities detected
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output
- Edge kinds: calls: 6141 · contains: 4982 · method: 3891 · MODIFIES: 3014 · imports: 2004 · ON_BRANCH: 836 · references: 731 · reads_from: 583 · imports_from: 521 · PARENT_OF: 426 · triggers: 136 · inherits: 99 · re_exports: 6


## Input Scope
- Requested: committed
- Resolved: committed (source: cli)
- Included files: 1113 · Candidates: 1533
- Excluded: 0 untracked · 49790 ignored · 1 sensitive · 0 missing committed
- Recommendation: Use --scope all or graphify.yaml inputs.corpus for a knowledge-base folder.

## Graph Freshness
- Built from Git commit: `5d23f6d`
- Compare this hash to `git rev-parse HEAD` before trusting freshness-sensitive graph output.
## God Nodes (most connected - your core abstractions)
1. `AdministrationRepository` - 233 edges
2. `PatientPortalRepository` - 215 edges
3. `clinicianGet()` - 162 edges
4. `clinicianPost()` - 135 edges
5. `TelehealthEndpoints` - 115 edges
6. `InventoryRepository` - 110 edges
7. `BillingRepository` - 102 edges
8. `json()` - 101 edges
9. `DocumentRepository` - 101 edges
10. `PatientRepository` - 95 edges

## Surprising Connections (you probably didn't know these)
- `access_user_memberships` --references--> `staff`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 464 → community 187_
- `appointments` --references--> `patients`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 187 → community 118_
- `inventory_lots` --references--> `facilities`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 431 → community 187_
- `lab_orders` --references--> `patients`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 317 → community 118_
- `lab_orders` --references--> `staff`  [EXTRACTED]
  avenchart/database/bootstrap/base-schema.sql → avenchart/database/bootstrap/base-schema.sql  _Bridges community 317 → community 187_

## Communities

### Community 0 - "Community 0"
Cohesion: 0.01
Nodes (301): OperationsState, addEncounterTrackReading(), AddressBookEntry, AdministrationAccessGroupItem, AdministrationAccessPermissionItem, AdministrationAccessPermissionMutationInput, AdministrationAccessPermissionMutationResponse, AdministrationAccessUserMembershipMutationInput (+293 more)

### Community 1 - "Community 1"
Cohesion: 0.02
Nodes (252): acknowledgeApplicantPreRequestReadiness(), acknowledgeApplicantTelehealthNotice(), applicantHeaders(), assessApplicantTelehealthRequestComplaintTriage(), assessApplicantTelehealthRequestUniversalSafety(), authorizeRequest(), commandInit(), completePatientReadiness() (+244 more)

### Community 2 - "Community 2"
Cohesion: 0.01
Nodes (32): 873ca5d feat: add governed telehealth foundation through sprint 39, telehealth_consultation_contexts, trg_telehealth_consultation_contexts_append_only, clearApplicantSession(), createApplicantAccessKey(), loadApplicantSession(), saveApplicantSession(), TelehealthApplicantSession (+24 more)

### Community 3 - "Community 3"
Cohesion: 0.01
Nodes (82): 4d9e669 Move referral workflow state to EF Core, 5d1561c fix(therapy): atomically link generated encounters, 6863261 Adopt EF Core for directory and education data, 827699e Move document template lifecycle to EF Core, a2b1800 Move therapy group state to EF Core, b4f43cb Move patient request and SDOH aggregates to EF Core, c65eb6d Split administration directory mutations into EF Core, cf6a3b2 Move recalls and chart tracking to EF Core (+74 more)

### Community 4 - "Community 4"
Cohesion: 0.04
Nodes (149): codex/ef-data-access-modernization, main, 0220d35 fix(auth): scope scheduling and encounter workflows to facility, 0239c9f fix(labs): invalidate stale critical result queue, 05beda1 refactor(api): isolate encounter endpoints, 05fa4fb fix(patients): make administration updates atomic, 07e8fdd fix(sdoh): anchor generated goals to assessment date, 083e6b1 fix(scheduling): enforce appointment concurrency (+141 more)

### Community 5 - "Community 5"
Cohesion: 0.02
Nodes (126): AccessMembershipForm, AccessPermissionForm, ApiClientForm, AsyncState, CodingCatalogForm, FacilityForm, UserForm, actions (+118 more)

### Community 6 - "Community 6"
Cohesion: 0.02
Nodes (24): ClinicianSession, loadClinicianSession(), updateClinicianSession(), Draft, State, validStatuses, loadNavigationScroll(), NAV_ITEMS (+16 more)

### Community 7 - "Community 7"
Cohesion: 0.03
Nodes (26): BackgroundService, 2cd0fa8 feat(security): govern identity and disclosure foundations, 431b12a chore: checkpoint AvenChart workspace, 4f39cd9 Publish AvenChart source and public history, 56a8a1a Adopt AvenChart product identity, ReportExecutionOptions, PatientEducationRepository, PatientReadingAccumulator (+18 more)

### Community 8 - "Community 8"
Cohesion: 0.04
Nodes (100): codex/appointment-scheduling, codex/local-docker-scripts, 0316b13 feat(procedures): protect locked encounter order entry, 03db92b fix(forms): validate legacy ROS compatibility sections, 04b5c83 feat(therapy): require recorded group attendance, 05ca2d7 feat(encounters): type locked track catalog, 0fb4655 feat(labs): govern specimen lifecycle, 10b1502 feat(ui): add referral work queue (+92 more)

### Community 9 - "Community 9"
Cohesion: 0.04
Nodes (74): 0035c04 chore: record sprint 41 evidence and code graph, 125e764 feat(telehealth): add synthetic participation evaluation, 173baf2 feat: confirm applicant telehealth request location, 32cb3f5 docs: finalize sprint 44 verification evidence, 3618e20 feat(telehealth): confirm participation prerequisite context, 38ec0f6 feat(telehealth): show applicant request queue status, 3cedeeb feat(telehealth): select rendering network candidate, 4e5c02a chore(graph): refresh Sprint 54 index (+66 more)

### Community 10 - "Community 10"
Cohesion: 0.02
Nodes (93): asEncounterLifecycleDetail(), EncounterSoapNoteConflict, EncounterSoapNoteConflictProblem, EncounterSoapNoteVersion, getEncounterSoapNoteConflict(), getVersionedEncounterDetail(), saveEncounterSoapNote(), SaveEncounterSoapNoteInput (+85 more)

### Community 11 - "Community 11"
Cohesion: 0.06
Nodes (1): TelehealthEndpoints

### Community 12 - "Community 12"
Cohesion: 0.04
Nodes (75): cancelGovernedReportRun(), createGovernedReportDefinition(), createGovernedReportRevision(), deleteGovernedReportDefinitionTestFixture(), downloadGovernedReportRun(), getGovernedReportCatalog(), getGovernedReportDefinition(), getGovernedReportDefinitions() (+67 more)

### Community 13 - "Community 13"
Cohesion: 0.02
Nodes (88): AddressBook(), blank(), empty, ClinicianOutletContext, AsyncState, PatientMessages(), statusClass(), PrintableOutput (+80 more)

### Community 14 - "Community 14"
Cohesion: 0.02
Nodes (83): AsyncState, FilterDraft, Activity, AsyncState, AuditState, PharmacyState, PrescriptionEditDraft, QueueView (+75 more)

### Community 15 - "Community 15"
Cohesion: 0.06
Nodes (1): BillingRepository

### Community 16 - "Community 16"
Cohesion: 0.07
Nodes (1): DocumentRepository

### Community 17 - "Community 17"
Cohesion: 0.02
Nodes (77): ExportEvidence, formatFilterSummary(), InventoryActivityPanel(), Props, ReportFilters, ReportRun, CatalogState, Props (+69 more)

### Community 18 - "Community 18"
Cohesion: 0.03
Nodes (77): BillingWorkspace(), money(), context, ClinicianMessages(), FilterDraft, filtersFromParams(), initials(), PatientThread (+69 more)

### Community 19 - "Community 19"
Cohesion: 0.06
Nodes (1): PatientRepository

### Community 20 - "Community 20"
Cohesion: 0.07
Nodes (1): AppointmentRepository

### Community 21 - "Community 21"
Cohesion: 0.07
Nodes (1): ProcedureRepository

### Community 22 - "Community 22"
Cohesion: 0.04
Nodes (50): ClinicianCalendar(), isoDate(), PALETTE, ProviderEntry, WEEKDAYS, AsyncState, ClinicianSchedule(), isoDate() (+42 more)

### Community 23 - "Community 23"
Cohesion: 0.05
Nodes (73): amendClinicalFormInstance(), ClinicalFormCondition, ClinicalFormDefinitionDetail, ClinicalFormDefinitionEvent, ClinicalFormDefinitionList, ClinicalFormEvaluation, ClinicalFormFieldDictionary, ClinicalFormFieldDictionaryItem (+65 more)

### Community 24 - "Community 24"
Cohesion: 0.04
Nodes (37): 022ba1c feat(forms): adopt legacy bronchitis sinus exam, 0383578 feat(forms): adopt legacy clinical instructions, 13b3e65 feat(forms): adopt legacy review systems pulmonary, 197a8b2 feat(forms): adopt legacy bronchitis cardiac exam, 1b619ab feat(forms): adopt legacy ankle diagnosis plan, 20b406c feat(forms): adopt legacy review systems endocrine, 2a6434a feat(forms): adopt legacy clinic note, 2d2b483 feat(forms): add governed clinical form engine (+29 more)

### Community 25 - "Community 25"
Cohesion: 0.03
Nodes (57): administrationAreaLabels, administrationFieldLabels, BLANK_INS, CareTeamDraft, CareTeamMemberDraft, careTeamRoleOptions, careTeamStatusOptions, fact() (+49 more)

### Community 26 - "Community 26"
Cohesion: 0.09
Nodes (2): DiagnosisAccumulator, EncounterRepository

### Community 27 - "Community 27"
Cohesion: 0.05
Nodes (1): PatientPortalRepository

### Community 28 - "Community 28"
Cohesion: 0.09
Nodes (1): ClinicalFormRepository

### Community 29 - "Community 29"
Cohesion: 0.06
Nodes (38): announceInvalidSession(), ApiErrorKind, apiFetch(), ApiRequestError, isInvalidSessionError(), isRequestCancellation(), materializeRequestHeaders(), parseProblemDetails() (+30 more)

### Community 30 - "Community 30"
Cohesion: 0.04
Nodes (43): LabReportAndResultCapture(), Props, today(), AsyncState, PatientLabs(), today(), LabResultFlag(), labResultFlagClass() (+35 more)

### Community 31 - "Community 31"
Cohesion: 0.07
Nodes (1): AdministrationRepository

### Community 32 - "Community 32"
Cohesion: 0.09
Nodes (1): ClinicalFormRuntime

### Community 33 - "Community 33"
Cohesion: 0.04
Nodes (39): AsyncState, ClinicianDashboard(), greeting(), RecentPatient, today(), ActionEditor, DocumentOcrQueue(), HistoryState (+31 more)

### Community 34 - "Community 34"
Cohesion: 0.06
Nodes (32): 07ec116 feat(admin): govern facility settings, 0e8f4e9 feat(forms): adopt legacy review systems genitourinary, 1cc9676 feat(admin): resolve facility settings, 247252b feat(admin): catalog practice settings, 3f10a90 feat(admin): add configuration package dry run, 41fb6b1 feat(forms): display legacy soap snapshots, 6144255 feat(admin): delegate facility setting drafts, 654ab2b feat(admin): add reviewed configuration imports (+24 more)

### Community 35 - "Community 35"
Cohesion: 0.10
Nodes (37): ClinicalFormCalculation, ClinicalFormCalculationTemplate, ClinicalFormField, ClinicalFormPolicy, ClinicalFormRule, actionsFor(), ClinicalFormGovernance(), Props (+29 more)

### Community 36 - "Community 36"
Cohesion: 0.05
Nodes (37): CriticalLabResultFollowUpLifecycleTests, 11c91a9 fix(labs): bind report reviews to result content, 32bb53e feat(labs): govern local report review lifecycle, 62913fa fix(labs): govern critical result follow-up lifecycle, 96d5d23 feat(labs): add local critical result acknowledgement, eaf0ce6 fix(labs): serialize order changes with encounter signing, CriticalLabResultFollowUpLifecycle, lab_report_review_events (+29 more)

### Community 37 - "Community 37"
Cohesion: 0.05
Nodes (45): formatDate(), InventoryControlledCountsPanel(), Props, addRecallActivity(), approveInventoryControlledCountAttestation(), approveInventoryControlledDiscrepancyCorrectionAttestation(), clinicianPost(), closeInventoryControlledDiscrepancy() (+37 more)

### Community 38 - "Community 38"
Cohesion: 0.05
Nodes (32): 08d116e Establish hybrid EF Core data access foundation, 0a3a419 Split encounter state mutations into EF Core, 21f29da fix(encounters): reject stale summary updates, 6a28154 fix(clinical): govern vital observation corrections, 7560638 fix(clinical): bind encounter signatures to content, 80ed212 feat(encounters): add SOAP version conflicts, edd15ef fix(encounters): serialize locking signatures and writes, AzureDeploymentProfileValidationException (+24 more)

### Community 39 - "Community 39"
Cohesion: 0.08
Nodes (33): BrowserOidcAudience, BrowserOidcConfiguration, BrowserOidcPortalSession, BrowserOidcStaffSession, getBrowserOidcConfiguration(), getBrowserOidcPortalSession(), getBrowserOidcStaffSession(), startBrowserOidcSignIn() (+25 more)

### Community 40 - "Community 40"
Cohesion: 0.08
Nodes (6): IDisposable, IHostedService, AzureCliRunner, AzureDeploymentCoordinator, AzureOperationsService, TemporaryParameterFile

### Community 41 - "Community 41"
Cohesion: 0.06
Nodes (24): 0819a56 feat(auth): add provider-neutral OIDC and test IdP, 231a478 feat(auth): govern external OIDC subject mappings, 72da57c fix(messages): govern content mutations with versions, ab3a3f9 feat(portal): support governed OIDC identity mappings, b03f736 fix(labs): make facility intake context explicit, eeb5f50 fix(identity): harden OIDC session interoperability, IdentityProviderOptions, StaffAuthenticationEndpoints (+16 more)

### Community 42 - "Community 42"
Cohesion: 0.09
Nodes (1): TelehealthRepository

### Community 43 - "Community 43"
Cohesion: 0.11
Nodes (1): ReportExecutionRepository

### Community 44 - "Community 44"
Cohesion: 0.07
Nodes (38): asEncounterCodingDetail(), BillingLineCreateInput, clinicianHeaders(), CompleteEncounterCreateInput, createCompleteEncounter(), createEncounterBillingLine(), EncounterBillingClaim, EncounterBillingLine (+30 more)

### Community 45 - "Community 45"
Cohesion: 0.06
Nodes (22): 077c7bc fix(clinical): block new content on inactive patients, 110de8a style(backend): apply C# formatter, 4183296 feat(patients): audit deceased status corrections, 5dda587 fix(prescriptions): retain records and audit evidence, 6b68b32 perf(patients): govern facility search plan, 72874b1 feat(clinical): retain immutable list mutation evidence, 737f5b7 fix(prescriptions): block continuation after lifecycle closure, a8cdf66 feat(patients): govern retirement lifecycle (+14 more)

### Community 46 - "Community 46"
Cohesion: 0.15
Nodes (1): ClinicalListRepository

### Community 47 - "Community 47"
Cohesion: 0.06
Nodes (13): AccessibilityFinding, clinicianFixture, codingEncounter, encounter, cleanupLifecycleFixture(), composeRoot, fixtureSql(), AvenChartUiFixtures (+5 more)

### Community 48 - "Community 48"
Cohesion: 0.10
Nodes (45): applicant_row, complaint_row, creation_row, enforce_th_app_request_intake_snapshot(), facilities, insurance_records, intake_row, location_confirmation_row (+37 more)

### Community 49 - "Community 49"
Cohesion: 0.09
Nodes (35): archiveAzureDeploymentProfile(), assessAzureDeploymentProfile(), AzureAccessValidationResponse, AzureDeploymentExecutionDetail, AzureDeploymentExecutionSummary, AzureDeploymentHealth, AzureDeploymentProfileAssessment, AzureDeploymentProfileDetail (+27 more)

### Community 50 - "Community 50"
Cohesion: 0.07
Nodes (23): getPatientPortalAppointmentsWithRequestHistory(), PatientPortalAppointmentRequestHistoryEvent, PatientPortalAppointmentRequestHistoryItem, PatientPortalAppointmentsWithRequestHistoryResponse, 1a4aff4 feat(portal): add appointment request history, AppointmentStatusBadge(), AppointmentCard(), AsyncState (+15 more)

### Community 51 - "Community 51"
Cohesion: 0.11
Nodes (40): actual_count, allergies, allergy_item_count, allergy_row, applicant_row, enforce_telehealth_applicant_health_history_information(), enforce_telehealth_health_history_information_topic_count(), enforce_telehealth_reported_health_history_topic_provenance() (+32 more)

### Community 52 - "Community 52"
Cohesion: 0.15
Nodes (23): ArgumentException, ExternalLaboratoryFhirValidationException, ExternalLaboratoryIntakeRepository, FirstReference(), Invalid(), Matches(), Parse(), ParseObservation() (+15 more)

### Community 53 - "Community 53"
Cohesion: 0.13
Nodes (1): MessageRepository

### Community 54 - "Community 54"
Cohesion: 0.09
Nodes (34): PracticeReviewAuthorizationDraft, PracticeReviewClaimDraft, PromotionDraft, QueueAuthorizationDraft, ReviewDraft, SyntheticPromotionDraft, authorizeApplicantPracticeReview(), authorizeApplicantRequestToQueue() (+26 more)

### Community 55 - "Community 55"
Cohesion: 0.06
Nodes (28): displayDate(), DraftForm, emptyDraft, MutableAuthorizationState, PatientAuthorizations(), titleCase(), FilterDraft, QueueState (+20 more)

### Community 56 - "Community 56"
Cohesion: 0.06
Nodes (26): formatCurrency(), InventoryDispensingPanel(), LotWithItem, PatientContextState, PatientSearchState, Props, Result, ALL_KINDS (+18 more)

### Community 57 - "Community 57"
Cohesion: 0.06
Nodes (30): CatalogForm, CatalogState, DirectoryState, clinicianDelete(), clinicianDeleteJson(), clinicianHeaders(), createProcedureLabProviderOrganization(), createProcedureOrderCatalogItem() (+22 more)

### Community 58 - "Community 58"
Cohesion: 0.08
Nodes (35): api_client_registry, auth_audit_events, clinical_alert_rules, coding_catalog_audit_events, coding_catalogs, dataset_metadata, encounter_audit_events, encounter_clinical_alert_acknowledgments (+27 more)

### Community 59 - "Community 59"
Cohesion: 0.09
Nodes (2): InventoryItemBuilder, InventoryRepository

### Community 60 - "Community 60"
Cohesion: 0.12
Nodes (35): actual_count, allergies, applicant_row, enforce_telehealth_allergy_information_item_count(), enforce_telehealth_applicant_allergy_information(), enforce_telehealth_reported_allergy_item_provenance(), facilities, insurance_records (+27 more)

### Community 61 - "Community 61"
Cohesion: 0.12
Nodes (35): applicant_row, creation_row, eligibility_row, enforce_th_app_request_insurance_source(), facilities, handoff_row, insurance_records, intake_row (+27 more)

### Community 62 - "Community 62"
Cohesion: 0.10
Nodes (22): actOnManagedRecord(), createManagedRecord(), deleteManagedRecordTestFixture(), getManagedRecordHistory(), getManagedRecordPolicy(), getManagedRecords(), headers(), ManagedRecordCreateInput (+14 more)

### Community 63 - "Community 63"
Cohesion: 0.13
Nodes (28): createPatientDisclosureAuthority(), createPatientDisclosureRequest(), decidePatientDisclosureRequest(), getPatientDisclosureAuthorities(), getPatientDisclosureAuthorityHistory(), getPatientDisclosurePolicy(), getPatientDisclosureRequestHistory(), getPatientDisclosureRequests() (+20 more)

### Community 64 - "Community 64"
Cohesion: 0.15
Nodes (1): ManagedRecordRepository

### Community 65 - "Community 65"
Cohesion: 0.10
Nodes (33): appointment_reminder_dispatch_audit, avenchart_integer_counters, lab_results, medication_vocabulary, patient_administration_audit_events, patient_document_archive_events, patient_document_content_events, patient_document_metadata_events (+25 more)

### Community 66 - "Community 66"
Cohesion: 0.07
Nodes (22): RuntimeSafetyPolicyTests, 06cf8a3 feat(fhir): validate external laboratory R4 profiles, 7be4153 feat(runtime): fail closed for production hosting, 7f7a66f fix(billing): isolate generated financial fixtures, e474fa1 fix(fhir): omit empty optional repeat fields, RuntimeSafetyOptions, net10.0, coverlet.collector (+14 more)

### Community 67 - "Community 67"
Cohesion: 0.12
Nodes (1): ReportRepository

### Community 69 - "Community 69"
Cohesion: 0.09
Nodes (13): 58f7374 Harden migrations and add review assessments, 74af4ef fix(labs): serialize procedure result corrections, 8d20f8c Fix clean-checkout migration verification, aa093cf Harden migration startup and schema readiness, bea1383 feat(database): bootstrap empty PostgreSQL schemas, f33a442 fix(migrations): harden empty database recovery, DatabaseBootstrapCatalog, SchemaMigrationFaultInjectionException (+5 more)

### Community 70 - "Community 70"
Cohesion: 0.15
Nodes (1): BrowserOidcSessionService

### Community 71 - "Community 71"
Cohesion: 0.15
Nodes (1): ReportDefinitionRepository

### Community 72 - "Community 72"
Cohesion: 0.14
Nodes (30): actual_count, applicant_row, enforce_telehealth_applicant_medication_information(), enforce_telehealth_medication_information_item_count(), enforce_telehealth_reported_medication_item_provenance(), facilities, insurance_records, inventory_row (+22 more)

### Community 73 - "Community 73"
Cohesion: 0.14
Nodes (30): allergies, allergy_count, allergy_row, applicant_row, enforce_telehealth_applicant_clinical_information_summary(), facilities, history_count, history_row (+22 more)

### Community 74 - "Community 74"
Cohesion: 0.23
Nodes (1): TelehealthConsultationServiceTests

### Community 75 - "Community 75"
Cohesion: 0.08
Nodes (25): AddMode, AsyncState, ClinicalAuditHistoryState, ClinicalAuditResourceType, LifecycleTarget, PatientChart(), today(), VocabularyState (+17 more)

### Community 76 - "Community 76"
Cohesion: 0.16
Nodes (28): allergies, applicant_row, communication_row, device_row, enforce_telehealth_applicant_pre_request_readiness(), facilities, insurance_records, insurance_row (+20 more)

### Community 77 - "Community 77"
Cohesion: 0.08
Nodes (12): 1e2a01d Replace global integer allocators with sequences, 73c3e09 Split procedure directory state into EF Core, 7502ada fix(labs): enforce catalog reference identities, LabOrderCatalogConfiguration, LabOrderReferenceConfiguration, LabProviderAddressBookConfiguration, LabProviderConfiguration, LabOrderCatalogEntity (+4 more)

### Community 78 - "Community 78"
Cohesion: 0.14
Nodes (3): Allowed(), Denied(), StaffAccessContextService

### Community 79 - "Community 79"
Cohesion: 0.07
Nodes (11): 0b706a0 Split clinical list state into EF Core, AllergyConfiguration, ImmunizationConfiguration, MedicationConfiguration, MedicationLifecycleEventConfiguration, ProblemConfiguration, AllergyEntity, ImmunizationEntity (+3 more)

### Community 81 - "Community 81"
Cohesion: 0.17
Nodes (26): applicant_row, assessment_row, creation_row, enforce_th_app_request_complaint_triage(), facilities, location_confirmation_row, location_row, patient_row (+18 more)

### Community 82 - "Community 82"
Cohesion: 0.16
Nodes (26): applicant_row, candidate_row, eligibility_row, enforce_th_app_request_part_context(), facilities, insurance_records, network_row, patient_row (+18 more)

### Community 83 - "Community 83"
Cohesion: 0.09
Nodes (16): AsyncState, SUBJECT_PRESETS, View, archivePatientPortalMessages(), composePatientPortalMessage(), deletePatientPortalMessage(), downloadPatientPortalMessageAttachment(), getPatientPortalMessageComposeOptions() (+8 more)

### Community 84 - "Community 84"
Cohesion: 0.09
Nodes (21): DetailState, DocumentTemplates(), formatDateTime(), pageCount(), selectedPage(), TemplateDraft, createDocumentTemplate(), DocumentTemplateBinaryVersion (+13 more)

### Community 85 - "Community 85"
Cohesion: 0.08
Nodes (24): AsyncState, formatDateTime(), PracticeSettingGovernance(), Props, statusBadgeClass(), statusLabels, createPracticeSettingChangeRequest(), EffectivePracticeSettingItem (+16 more)

### Community 86 - "Community 86"
Cohesion: 0.10
Nodes (8): 2a53ba9 feat(labs): scope external sources to facilities, 32d97a0 feat(labs): ingest profiled FHIR laboratory results, a9eec9f fix(fhir): make search contract pageable and typed, bc6cc4d feat(labs): govern external laboratory source credentials, FhirResults, external_laboratory_source_events, external_laboratory_sources, trg_external_laboratory_source_events_immutable

### Community 87 - "Community 87"
Cohesion: 0.21
Nodes (1): AuthorizationRepository

### Community 88 - "Community 88"
Cohesion: 0.18
Nodes (1): IntegrationIdempotencyConflictException

### Community 89 - "Community 89"
Cohesion: 0.22
Nodes (1): PatientDisclosureRepository

### Community 90 - "Community 90"
Cohesion: 0.17
Nodes (25): applicant_row, assessment_row, creation_row, enforce_th_app_request_universal_safety(), facilities, location_confirmation_row, location_row, patient_row (+17 more)

### Community 91 - "Community 91"
Cohesion: 0.16
Nodes (25): applicant_row, candidate_row, context_row, enforce_th_app_request_part_eval(), facilities, insurance_records, patient_row, patients (+17 more)

### Community 93 - "Community 93"
Cohesion: 0.15
Nodes (1): AzureOperationsRepository

### Community 94 - "Community 94"
Cohesion: 0.28
Nodes (1): ClinicalListStateRepository

### Community 95 - "Community 95"
Cohesion: 0.23
Nodes (1): FhirRepository

### Community 96 - "Community 96"
Cohesion: 0.14
Nodes (3): PatientEncounterAccumulator, PatientTrackAccumulator, TrackAnythingRepository

### Community 97 - "Community 97"
Cohesion: 0.16
Nodes (1): AzureDeploymentProfilePolicy

### Community 98 - "Community 98"
Cohesion: 0.17
Nodes (24): applicant_row, candidate_row, eligibility_row, enforce_th_app_request_render_candidate(), facilities, insurance_records, network_row, patient_row (+16 more)

### Community 99 - "Community 99"
Cohesion: 0.17
Nodes (24): actor_row, applicant_row, candidate_row, enforce_th_app_request_queue_authorization(), facilities, insurance_records, new.applicant_id, patient_row (+16 more)

### Community 100 - "Community 100"
Cohesion: 0.18
Nodes (23): applicant_row, enforce_th_app_request_eligibility(), facilities, insurance_records, member_row, new.group_number_last4, patient_row, patients (+15 more)

### Community 102 - "Community 102"
Cohesion: 0.24
Nodes (1): ReferralRepository

### Community 103 - "Community 103"
Cohesion: 0.19
Nodes (22): applicant_row, eligibility_row, enforce_telehealth_insurance_handoff_confirmation(), facilities, insurance_records, member_row, member_row.group_number_last4, network_row (+14 more)

### Community 104 - "Community 104"
Cohesion: 0.19
Nodes (22): applicant_row, candidate_row, enforce_th_app_request_op_review_submission(), evaluation_row, facilities, insurance_records, patient_row, patients (+14 more)

### Community 105 - "Community 105"
Cohesion: 0.23
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsPolicyTests

### Community 106 - "Community 106"
Cohesion: 0.22
Nodes (1): TelehealthService

### Community 107 - "Community 107"
Cohesion: 0.15
Nodes (20): ClinicalFormSchema, ClinicalFormSection, canonicalJson(), ClinicalFormChangeImpact, ClinicalFormImpactItem, ClinicalFormImpactSeverity, compareField(), compareFields() (+12 more)

### Community 109 - "Community 109"
Cohesion: 0.16
Nodes (1): DocumentTemplateRepository

### Community 110 - "Community 110"
Cohesion: 0.22
Nodes (1): PatientMergeExecutionRepository

### Community 111 - "Community 111"
Cohesion: 0.20
Nodes (2): ReportExecutionQueueRepository, WorkerCancellationState

### Community 113 - "Community 113"
Cohesion: 0.22
Nodes (1): InventoryReplenishmentPolicyRepository

### Community 114 - "Community 114"
Cohesion: 0.21
Nodes (20): applicant_row, enforce_telehealth_applicant_device_preparation(), facilities, handoff_row, insurance_records, patient_row, patients, promotion_row (+12 more)

### Community 115 - "Community 115"
Cohesion: 0.19
Nodes (20): applicant_row, authorization_row, enforce_telehealth_applicant_request_creation(), facilities, new.source_applicant_id, new.source_practice_review_authorization_id, new.source_practice_review_case_id, new.source_promotion_id (+12 more)

### Community 116 - "Community 116"
Cohesion: 0.20
Nodes (20): applicant_row, eligibility_row, enforce_th_app_request_practice_network(), facilities, insurance_records, patient_row, patients, request_row (+12 more)

### Community 117 - "Community 117"
Cohesion: 0.20
Nodes (1): TelehealthApplicantPreRequestReadinessRepository

### Community 118 - "Community 118"
Cohesion: 0.13
Nodes (20): allergies, claims, clinical_notes, insurance_records, messages, patient_employers, patient_histories, patient_portal_accounts (+12 more)

### Community 121 - "Community 121"
Cohesion: 0.19
Nodes (6): IEndpointFilter, AzureOperationsAccessFilter, AzureOperationsAccessLockedException, AzureOperationsAccessService, AzureOperationsEnabledFilter, UnauthorizedAccessException

### Community 122 - "Community 122"
Cohesion: 0.23
Nodes (19): applicant_row, details_row, eligibility_row, enforce_telehealth_applicant_practice_network_determination(), facilities, precheck_row, purpose_row, review_row (+11 more)

### Community 123 - "Community 123"
Cohesion: 0.23
Nodes (19): allergies, applicant_row, case_row, enforce_telehealth_applicant_practice_review_submission(), facilities, insurance_records, medications, patient_row (+11 more)

### Community 124 - "Community 124"
Cohesion: 0.22
Nodes (19): applicant_row, creation_row, enforce_telehealth_applicant_request_location_confirmation(), facilities, location_row, patient_row, patients, readiness_row (+11 more)

### Community 125 - "Community 125"
Cohesion: 0.19
Nodes (1): TelehealthConsultationRepository

### Community 126 - "Community 126"
Cohesion: 0.15
Nodes (8): CreateOptions, FixtureCleanup, FixtureId, FixtureReset, LifecycleDomain, lifecycleDomains, LifecycleFixtureRegistry, LifecycleRecord

### Community 127 - "Community 127"
Cohesion: 0.15
Nodes (14): addTherapyGroupMember(), createTherapyGroup(), createTherapyGroupSession(), createTherapyGroupSessionEncounters(), getTherapyGroupMembers(), getTherapyGroups(), getTherapyGroupSessionAttendance(), getTherapyGroupSessions() (+6 more)

### Community 128 - "Community 128"
Cohesion: 0.21
Nodes (1): AdministrationDirectoryRepository

### Community 130 - "Community 130"
Cohesion: 0.21
Nodes (2): AuthRepository, ToResponse()

### Community 131 - "Community 131"
Cohesion: 0.25
Nodes (1): InventoryCostPolicyRepository

### Community 133 - "Community 133"
Cohesion: 0.26
Nodes (1): ProcedureDirectoryRepository

### Community 134 - "Community 134"
Cohesion: 0.23
Nodes (18): applicant_row, details_row, enforce_telehealth_applicant_eligibility_result(), facilities, new.group_number_last4, precheck_row, purpose_row, review_row (+10 more)

### Community 135 - "Community 135"
Cohesion: 0.23
Nodes (18): applicant_row, enforce_telehealth_communication_access_readiness(), facilities, handoff_row, insurance_records, patient_row, patients, promotion_row (+10 more)

### Community 136 - "Community 136"
Cohesion: 0.22
Nodes (18): applicant_row, enforce_telehealth_applicant_clinical_information_inventory(), facilities, insurance_records, patient_row, patients, preparation_row, promotion_row (+10 more)

### Community 137 - "Community 137"
Cohesion: 0.13
Nodes (16): AsyncState, catalogSummary(), CodingCatalogGovernance(), formatDateTime(), Props, statusBadgeClass(), statusLabels, CodingCatalogChangeRequestAction (+8 more)

### Community 138 - "Community 138"
Cohesion: 0.28
Nodes (1): ExternalLaboratorySourceRepository

### Community 139 - "Community 139"
Cohesion: 0.27
Nodes (1): InventoryAccountingIntegrationRepository

### Community 142 - "Community 142"
Cohesion: 0.22
Nodes (17): applicant_row, enforce_telehealth_applicant_promotion_authorization(), facilities, proofing_row, staff, telehealth_applicant_eligibility_results, telehealth_applicant_identity_proofing_results, telehealth_applicant_identity_review_decisions (+9 more)

### Community 143 - "Community 143"
Cohesion: 0.29
Nodes (1): TelehealthApplicantRequestParticipationEvaluationRepository

### Community 144 - "Community 144"
Cohesion: 0.18
Nodes (14): ExperienceAnalyticsEvent, ExperienceBaseline, ExperienceBaselineCounts, ExperienceCriterion, ExperienceEnvironment, ExperienceGap, ExperienceRole, ExperienceTask (+6 more)

### Community 146 - "Community 146"
Cohesion: 0.14
Nodes (1): ToInventoryLot()

### Community 147 - "Community 147"
Cohesion: 0.14
Nodes (1): ToResponse()

### Community 148 - "Community 148"
Cohesion: 0.25
Nodes (1): TherapyGroupRepository

### Community 149 - "Community 149"
Cohesion: 0.18
Nodes (9): cleanupPracticeSettingGovernanceFixtures(), deleteClinicalListFixture(), deletePatientAdministrationFixtures(), deletePatientDocumentFixtures(), deletePrescriptionFixture(), deleteProcedureOrderFixture(), deleteProviderAssignmentFixtures(), deleteStaffMessageFixture() (+1 more)

### Community 150 - "Community 150"
Cohesion: 0.24
Nodes (16): appointments, capture_patient_portal_appointment_request(), new.appointment_date, new.category_id, new.comments, new.duration_minutes, new.facility_id, new.provider_id (+8 more)

### Community 151 - "Community 151"
Cohesion: 0.25
Nodes (15): facilities, patients, staff, telehealth_clinician_shifts, telehealth_patient_locations, telehealth_protocol_versions, telehealth_queue_entries, telehealth_request_events (+7 more)

### Community 152 - "Community 152"
Cohesion: 0.25
Nodes (16): applicant_row, case_row, claim_row, enforce_telehealth_practice_review_authorization(), facilities, patients, staff, submission_row (+8 more)

### Community 153 - "Community 153"
Cohesion: 0.24
Nodes (1): TelehealthApplicantAllergyInformationRepository

### Community 154 - "Community 154"
Cohesion: 0.24
Nodes (1): TelehealthApplicantClinicalInformationSummaryRepository

### Community 155 - "Community 155"
Cohesion: 0.24
Nodes (1): TelehealthApplicantCommunicationAccessRepository

### Community 156 - "Community 156"
Cohesion: 0.24
Nodes (1): TelehealthApplicantHealthHistoryInformationRepository

### Community 157 - "Community 157"
Cohesion: 0.24
Nodes (1): TelehealthApplicantMedicationInformationRepository

### Community 158 - "Community 158"
Cohesion: 0.30
Nodes (1): TelehealthApplicantRequestParticipationContextRepository

### Community 159 - "Community 159"
Cohesion: 0.30
Nodes (1): TelehealthApplicantRequestRenderingCandidateRepository

### Community 160 - "Community 160"
Cohesion: 0.27
Nodes (1): TelehealthProspectiveApplicantRepository

### Community 161 - "Community 161"
Cohesion: 0.17
Nodes (12): getIdentityProviderReadiness(), IdentityAdapterContract, IdentityBoundaryControl, IdentityProviderGap, IdentityProviderReadiness, IdentityProviderReadinessCounts, IdentityTypeReadiness, IdentityVerification (+4 more)

### Community 163 - "Community 163"
Cohesion: 0.32
Nodes (1): EncounterStateRepository

### Community 165 - "Community 165"
Cohesion: 0.28
Nodes (15): applicant_row, enforce_telehealth_applicant_member_insurance_details(), facilities, precheck_row, purpose_row, review_row, safety_row, telehealth_applicant_identity_review_decisions (+7 more)

### Community 166 - "Community 166"
Cohesion: 0.25
Nodes (15): applicant_row, enforce_telehealth_applicant_identity_proofing_result(), facilities, network_row, telehealth_applicant_eligibility_results, telehealth_applicant_identity_proofing_results, telehealth_applicant_identity_review_decisions, telehealth_applicant_member_insurance_details (+7 more)

### Community 167 - "Community 167"
Cohesion: 0.13
Nodes (1): TelehealthApplicantAllergyInformationPolicyTests

### Community 168 - "Community 168"
Cohesion: 0.25
Nodes (1): TelehealthApplicantClinicalInformationInventoryRepository

### Community 169 - "Community 169"
Cohesion: 0.25
Nodes (1): TelehealthApplicantDevicePreparationRepository

### Community 170 - "Community 170"
Cohesion: 0.13
Nodes (1): TelehealthApplicantHealthHistoryInformationPolicyTests

### Community 171 - "Community 171"
Cohesion: 0.13
Nodes (1): TelehealthApplicantMedicationInformationPolicyTests

### Community 172 - "Community 172"
Cohesion: 0.34
Nodes (1): TelehealthApplicantRequestComplaintTriageRepository

### Community 173 - "Community 173"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestEligibilityRepository

### Community 174 - "Community 174"
Cohesion: 0.34
Nodes (1): TelehealthApplicantRequestIntakeRepository

### Community 175 - "Community 175"
Cohesion: 0.32
Nodes (1): TelehealthApplicantRequestPracticeNetworkRepository

### Community 176 - "Community 176"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestUniversalSafetyRepository

### Community 177 - "Community 177"
Cohesion: 0.23
Nodes (1): TelehealthConsultationService

### Community 178 - "Community 178"
Cohesion: 0.25
Nodes (12): ClinicalFormDefinitionSummary, ClinicalFormFieldLocalization, ClinicalFormLocalization, createClinicalFormLocalization(), flattenFields(), flattenRules(), localizeClinicalFormSchema(), localizeClinicalFormSummary() (+4 more)

### Community 179 - "Community 179"
Cohesion: 0.27
Nodes (1): InventoryValuationRepository

### Community 180 - "Community 180"
Cohesion: 0.26
Nodes (3): IHostEnvironment, TelehealthRuntimeSafetyPolicyTests, TestHostEnvironment

### Community 181 - "Community 181"
Cohesion: 0.26
Nodes (14): facilities, managed_record_intake_events, managed_record_intakes, patient_document_archive_events, patient_document_content_events, patient_document_metadata_events, patient_document_ocr_events, patient_document_ocr_tasks (+6 more)

### Community 182 - "Community 182"
Cohesion: 0.27
Nodes (1): TelehealthApplicantInsuranceHandoffRepository

### Community 183 - "Community 183"
Cohesion: 0.36
Nodes (1): TelehealthApplicantRequestInsuranceSourceRepository

### Community 184 - "Community 184"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestOperationalReviewSubmissionRepository

### Community 185 - "Community 185"
Cohesion: 0.14
Nodes (3): TelehealthCommandFingerprint, TelehealthProblem, TelehealthRequestStateMachine

### Community 186 - "Community 186"
Cohesion: 0.15
Nodes (5): PhiAuditResourceContextTests, f95ef06 feat(audit): correlate direct PHI access resources, PhiAuditedResult, PhiAuditResourceContext, IResult

### Community 187 - "Community 187"
Cohesion: 0.22
Nodes (14): appointments, auth_accounts, auth_sessions, billing, encounter_signatures, encounters, facilities, immunizations (+6 more)

### Community 188 - "Community 188"
Cohesion: 0.15
Nodes (11): initialDefinition, InventoryCostPolicyGovernancePanel(), labelForMethod(), Props, createInventoryCostPolicyChangeRequest(), getInventoryCostPolicies(), getInventoryCostPolicyChangeRequest(), InventoryCostPolicyChangeRequest (+3 more)

### Community 191 - "Community 191"
Cohesion: 0.35
Nodes (1): PatientSdohRepository

### Community 192 - "Community 192"
Cohesion: 0.36
Nodes (1): AvenChartOpenApi

### Community 193 - "Community 193"
Cohesion: 0.22
Nodes (13): allergies, avenchart_require_active_patient_for_new_clinical_content(), immunizations, medications, patient_record, patients, prescriptions, problems (+5 more)

### Community 194 - "Community 194"
Cohesion: 0.31
Nodes (13): applicant_row, enforce_telehealth_applicant_practice_network_precheck(), facilities, purpose_row, review_row, safety_row, telehealth_applicant_identity_review_decisions, telehealth_applicant_practice_network_prechecks (+5 more)

### Community 195 - "Community 195"
Cohesion: 0.30
Nodes (13): applicant_row, authorization_row, current_match, enforce_telehealth_applicant_synthetic_promotion(), facilities, patient_row, patients, staff (+5 more)

### Community 196 - "Community 196"
Cohesion: 0.31
Nodes (13): applicant_row, enforce_telehealth_applicant_notice_acknowledgment(), facilities, patient_row, patients, promotion_row, safety_row, telehealth_applicant_notice_acknowledgments (+5 more)

### Community 197 - "Community 197"
Cohesion: 0.31
Nodes (13): applicant_row, enforce_telehealth_registration_details_confirmation(), facilities, notice_row, patient_row, patients, promotion_row, telehealth_applicant_notice_acknowledgments (+5 more)

### Community 198 - "Community 198"
Cohesion: 0.30
Nodes (1): TelehealthPrescriptionRepository

### Community 199 - "Community 199"
Cohesion: 0.14
Nodes (12): areaTotals, commits, here, historyBasePath, historyRef, log, monthly, outputPath (+4 more)

### Community 200 - "Community 200"
Cohesion: 0.15
Nodes (8): DOMAINS, STATUS_OPTIONS, createPatientSdohAssessment(), getPatientSdohAssessments(), PatientSdohAssessment, PatientSdohAssessmentInput, PatientSdohDomainValue, updatePatientSdohAssessment()

### Community 201 - "Community 201"
Cohesion: 0.24
Nodes (8): 2b91a4a feat(integrations): govern outbox recovery, 66cc16f feat(integrations): govern inbox reconciliation, 9fd53c1 feat(integrations): recover expired dispatch leases, d420fed feat(integrations): expose inbox decision history, integration_outbox, integration_outbox_events, integration_inbox, integration_inbox_events

### Community 202 - "Community 202"
Cohesion: 0.29
Nodes (11): external_laboratory_ingestion_events, external_laboratory_ingestions, external_laboratory_report_links, external_laboratory_result_links, external_laboratory_sources, lab_orders, lab_reports, lab_results (+3 more)

### Community 203 - "Community 203"
Cohesion: 0.28
Nodes (12): an, avenchart_capture_lab_report_review_content(), lab_report_review_events, lab_reports, lab_results, old.content_checksum, old.content_manifest, old.content_revision (+4 more)

### Community 204 - "Community 204"
Cohesion: 0.32
Nodes (12): insurance_records, telehealth_coverage_selections, telehealth_coverage_verifications, telehealth_demonstration_acknowledgments, telehealth_intake_snapshots, telehealth_patient_confirmations, telehealth_requests, trg_telehealth_coverage_selections_append_only (+4 more)

### Community 205 - "Community 205"
Cohesion: 0.31
Nodes (11): facilities, telehealth_requests, telehealth_reservations, telehealth_video_events, telehealth_video_participant_grants, telehealth_video_preflights, telehealth_video_sessions, trg_telehealth_video_events_append_only (+3 more)

### Community 206 - "Community 206"
Cohesion: 0.33
Nodes (12): appointments, encounters, facilities, staff, telehealth_clinician_shifts, telehealth_consultation_contexts, telehealth_consultation_events, telehealth_requests (+4 more)

### Community 207 - "Community 207"
Cohesion: 0.31
Nodes (12): catalog_row, encounters, enforce_telehealth_prescription_draft_catalog(), medication_vocabulary, staff, telehealth_consultation_contexts, telehealth_consultation_pharmacy_choice_versions, telehealth_consultation_prescription_draft_events (+4 more)

### Community 208 - "Community 208"
Cohesion: 0.18
Nodes (3): New-ComplaintBody(), New-MigraineAnswers(), New-SleepAnswers()

### Community 209 - "Community 209"
Cohesion: 0.42
Nodes (1): TelehealthApplicantRequestLocationRepository

### Community 210 - "Community 210"
Cohesion: 0.40
Nodes (1): TelehealthApplicantRequestQueueAuthorizationRepository

### Community 211 - "Community 211"
Cohesion: 0.28
Nodes (1): TelehealthApplicantSyntheticPromotionRepository

### Community 212 - "Community 212"
Cohesion: 0.28
Nodes (2): TelehealthDispositionRepository, TelehealthSafetyDispositionConflictException

### Community 213 - "Community 213"
Cohesion: 0.28
Nodes (1): TelehealthVideoRepository

### Community 214 - "Community 214"
Cohesion: 0.30
Nodes (2): 0701dc1 Merge pull request #1 from nkimber/codex/local-docker-scripts, 286a7d3 Add local Docker management scripts

### Community 215 - "Community 215"
Cohesion: 0.26
Nodes (9): 74ad7be feat(inventory): record costing exceptions, 9ea2933 feat(inventory): show cost layer applications, a006584 feat(inventory): cost patient sale movements, b593a6b feat(inventory): flag unallocated transfer costs, cdf5f2b feat(inventory): expose cost layer applications, f80119f feat(inventory): cost destruction movements, inventory_costing_exceptions, inventory_lots (+1 more)

### Community 216 - "Community 216"
Cohesion: 0.24
Nodes (1): AzureOperationsAccessRepository

### Community 217 - "Community 217"
Cohesion: 0.33
Nodes (1): BatchCommunicationRepository

### Community 218 - "Community 218"
Cohesion: 0.38
Nodes (1): LegacyClinicalFormDisplayRepository

### Community 219 - "Community 219"
Cohesion: 0.30
Nodes (1): EndpointAccessPolicies

### Community 220 - "Community 220"
Cohesion: 0.32
Nodes (11): applicant_row, enforce_telehealth_applicant_identity_review_decision(), facilities, new.contact_verified_at_snapshot, new.duplicate_disposition_snapshot, new.duplicate_evidence_fingerprint_snapshot, staff, telehealth_applicant_identity_review_decisions (+3 more)

### Community 221 - "Community 221"
Cohesion: 0.35
Nodes (11): applicant_row, enforce_telehealth_applicant_visit_purpose(), facilities, review_row, safety_row, telehealth_applicant_identity_review_decisions, telehealth_applicant_safety_triage_evaluations, telehealth_applicant_visit_purposes (+3 more)

### Community 222 - "Community 222"
Cohesion: 0.21
Nodes (5): Get-Counts(), Invoke-Scalar(), New-Key(), New-PrecheckedApplicant(), New-Secret()

### Community 223 - "Community 223"
Cohesion: 0.21
Nodes (5): Get-Counts(), Invoke-Scalar(), New-Key(), New-Secret(), New-VisitPurposeApplicant()

### Community 224 - "Community 224"
Cohesion: 0.21
Nodes (5): Get-Counts(), Invoke-Scalar(), New-ApprovedApplicant(), New-Key(), New-Secret()

### Community 225 - "Community 225"
Cohesion: 0.21
Nodes (5): Get-Counts(), Invoke-Scalar(), New-Key(), New-SafetyPassedApplicant(), New-Secret()

### Community 226 - "Community 226"
Cohesion: 0.20
Nodes (1): TelehealthApplicantRequestComplaintTriagePolicy

### Community 227 - "Community 227"
Cohesion: 0.27
Nodes (1): TelehealthApplicantRequestComplaintTriagePolicyTests

### Community 228 - "Community 228"
Cohesion: 0.30
Nodes (1): TelehealthApplicantRequestEligibilityService

### Community 229 - "Community 229"
Cohesion: 0.24
Nodes (2): IPharmacyDirectory, SyntheticTelehealthPharmacyDirectory

### Community 230 - "Community 230"
Cohesion: 0.33
Nodes (1): TelehealthPharmacyRepository

### Community 231 - "Community 231"
Cohesion: 0.45
Nodes (1): TelehealthPrescriptionServiceTests

### Community 232 - "Community 232"
Cohesion: 0.38
Nodes (1): TelehealthVideoService

### Community 233 - "Community 233"
Cohesion: 0.18
Nodes (4): 18dd71c feat(forms): adopt legacy speech dictation, 3fbf1fd feat(forms): adopt legacy phq9 screening, 7ad81c7 feat(forms): adopt legacy gad7 screening, aebec7a feat(forms): adopt legacy transfer summary

### Community 234 - "Community 234"
Cohesion: 0.22
Nodes (5): AppErrorBoundary, Component, createErrorReference(), Props, State

### Community 235 - "Community 235"
Cohesion: 0.44
Nodes (1): ClinicalAlertEvaluationRepository

### Community 236 - "Community 236"
Cohesion: 0.36
Nodes (1): EncounterLayoutFormRepository

### Community 237 - "Community 237"
Cohesion: 0.40
Nodes (1): ExternalIdentityMappingRepository

### Community 238 - "Community 238"
Cohesion: 0.40
Nodes (1): PatientPortalExternalIdentityMappingRepository

### Community 239 - "Community 239"
Cohesion: 0.51
Nodes (1): PatientPrintRepository

### Community 240 - "Community 240"
Cohesion: 0.44
Nodes (1): PatientXmlExchangeRepository

### Community 241 - "Community 241"
Cohesion: 0.35
Nodes (10): avenchart_reject_locked_encounter_mutation(), encounter_signatures, encounter_track_records, encounters, is_locked, lab_orders, lab_reports, or (+2 more)

### Community 242 - "Community 242"
Cohesion: 0.33
Nodes (9): facilities, telehealth_applicant_contact_challenges, telehealth_applicant_events, telehealth_applicant_verification_attempts, telehealth_prospective_applicants, trg_telehealth_applicant_attempts_append_only, trg_telehealth_applicant_challenges_append_only, trg_telehealth_applicant_events_append_only (+1 more)

### Community 243 - "Community 243"
Cohesion: 0.36
Nodes (10): facilities, patients, staff, telehealth_consultation_contexts, telehealth_consultation_pharmacy_choice_events, telehealth_consultation_pharmacy_choice_versions, telehealth_patient_pharmacy_preferences, trg_telehealth_consultation_pharmacy_choice_events_append_only (+2 more)

### Community 244 - "Community 244"
Cohesion: 0.24
Nodes (5): Applicant-Queue-Status-Path(), Get-Applicant-Queue-Status(), Invoke-ContendedQueueAuthorizations(), Post-Queue-Authorization(), Queue-Authorization-Path()

### Community 245 - "Community 245"
Cohesion: 0.24
Nodes (5): Get-Counts(), Invoke-Scalar(), New-AuthorizedApplicant(), New-Key(), New-Secret()

### Community 246 - "Community 246"
Cohesion: 0.24
Nodes (5): Get-Counts(), Invoke-Scalar(), New-EligibilityReadyApplicant(), New-Key(), New-Secret()

### Community 247 - "Community 247"
Cohesion: 0.24
Nodes (5): Get-Counts(), Invoke-Scalar(), New-Key(), New-NetworkedApplicant(), New-Secret()

### Community 248 - "Community 248"
Cohesion: 0.24
Nodes (5): Get-Counts(), Invoke-Scalar(), New-Key(), New-NetworkReadyApplicant(), New-Secret()

### Community 249 - "Community 249"
Cohesion: 0.20
Nodes (2): Scalar(), Sql-Fails()

### Community 250 - "Community 250"
Cohesion: 0.25
Nodes (7): getTelehealthPrescriptionPreparationDraft(), recordTelehealthPrescriptionPreparationDraft(), TelehealthPrescriptionPreparationDraft, TelehealthPrescriptionPreparationWorkspace, DraftFields, emptyFields, Props

### Community 251 - "Community 251"
Cohesion: 0.31
Nodes (1): TelehealthApplicantDevicePreparationPolicyTests

### Community 252 - "Community 252"
Cohesion: 0.22
Nodes (1): TelehealthApplicantPracticeReviewSubmissionPolicyTests

### Community 253 - "Community 253"
Cohesion: 0.20
Nodes (1): TelehealthApplicantPreRequestReadinessPolicyTests

### Community 254 - "Community 254"
Cohesion: 0.44
Nodes (1): TelehealthApplicantRequestCreationRepository

### Community 255 - "Community 255"
Cohesion: 0.25
Nodes (1): TelehealthApplicantRequestParticipationContextPolicyTests

### Community 256 - "Community 256"
Cohesion: 0.25
Nodes (1): TelehealthApplicantRequestParticipationEvaluationPolicyTests

### Community 257 - "Community 257"
Cohesion: 0.29
Nodes (1): TelehealthApplicantRequestQueueAuthorizationPolicyTests

### Community 258 - "Community 258"
Cohesion: 0.25
Nodes (1): TelehealthApplicantRequestRenderingCandidatePolicyTests

### Community 259 - "Community 259"
Cohesion: 0.18
Nodes (1): TelehealthApplicantRequestUniversalSafetyPolicy

### Community 260 - "Community 260"
Cohesion: 0.24
Nodes (1): TelehealthProspectiveApplicantPolicy

### Community 261 - "Community 261"
Cohesion: 0.31
Nodes (1): TelehealthProspectiveEligibilityService

### Community 262 - "Community 262"
Cohesion: 0.33
Nodes (1): TelehealthSafetyDispositionRulesTests

### Community 263 - "Community 263"
Cohesion: 0.31
Nodes (1): FhirR4ValidationServiceTests

### Community 264 - "Community 264"
Cohesion: 0.22
Nodes (8): AsyncState, AuthorizationPolicyRegistry(), formatGap(), gapOptions, AuthorizationPolicyCatalogResponse, AuthorizationPolicyGap, AuthorizationPolicyRule, getAuthorizationPolicyCatalog()

### Community 265 - "Community 265"
Cohesion: 0.20
Nodes (3): 10b94e5 feat(forms): adopt legacy ankle assessment, 9d6a4b8 feat(forms): adopt legacy treatment plan, da2d960 feat(forms): adopt legacy physical exam lines

### Community 266 - "Community 266"
Cohesion: 0.38
Nodes (1): DatabaseSchemaMigrator

### Community 267 - "Community 267"
Cohesion: 0.33
Nodes (9): allergies, avenchart_advance_allergy_review_state(), avenchart_initialize_allergy_review_state(), can, old.pid, patient_allergy_review_states, patients, trg_allergies_advance_review_state (+1 more)

### Community 268 - "Community 268"
Cohesion: 0.40
Nodes (9): applicant_row, enforce_telehealth_applicant_safety_triage_evaluation(), facilities, review_row, telehealth_applicant_identity_review_decisions, telehealth_applicant_safety_triage_evaluations, telehealth_prospective_applicants, trg_telehealth_applicant_safety_triage_append_only (+1 more)

### Community 269 - "Community 269"
Cohesion: 0.38
Nodes (9): applicant_row, case_row, enforce_telehealth_practice_review_claim(), facilities, telehealth_practice_review_claims, telehealth_prospective_applicants, telehealth_prospective_practice_review_cases, trg_enforce_telehealth_practice_review_claim (+1 more)

### Community 270 - "Community 270"
Cohesion: 0.20
Nodes (10): Archive-DocumentTestFixture(), Archive-EncounterTestFixture(), Archive-MessageTestFixture(), Cancel-AppointmentTestFixture(), Get-AdministrationHeaders(), New-AuthenticatedHttpClient(), New-ReceivedProcedureSpecimen(), Set-AdministrationFacilityContext() (+2 more)

### Community 271 - "Community 271"
Cohesion: 0.27
Nodes (5): Get-Counts(), Invoke-Scalar(), New-Key(), New-Secret(), New-VerifiedApplicant()

### Community 272 - "Community 272"
Cohesion: 0.27
Nodes (5): Get-Counts(), Invoke-Scalar(), New-Key(), New-ProofedApplicant(), New-Secret()

### Community 273 - "Community 273"
Cohesion: 0.31
Nodes (7): getTelehealthSafetyDispositionDraft(), recordTelehealthSafetyDispositionDraft(), TelehealthSafetyDispositionDraft, TelehealthSafetyDispositionWorkspace, humanize(), Props, TelehealthSafetyDispositionPanel()

### Community 274 - "Community 274"
Cohesion: 0.38
Nodes (1): SyntheticTelehealthComplaintTriageEvaluatorTests

### Community 275 - "Community 275"
Cohesion: 0.20
Nodes (1): TelehealthApplicantClinicalInformationInventoryPolicyTests

### Community 276 - "Community 276"
Cohesion: 0.22
Nodes (1): TelehealthApplicantClinicalInformationSummaryPolicyTests

### Community 277 - "Community 277"
Cohesion: 0.33
Nodes (1): TelehealthApplicantCommunicationAccessPolicyTests

### Community 278 - "Community 278"
Cohesion: 0.31
Nodes (1): TelehealthApplicantInsuranceHandoffPolicyTests

### Community 279 - "Community 279"
Cohesion: 0.29
Nodes (1): TelehealthApplicantNoticePolicyTests

### Community 280 - "Community 280"
Cohesion: 0.36
Nodes (1): TelehealthApplicantPromotionAuthorizationRepository

### Community 281 - "Community 281"
Cohesion: 0.40
Nodes (1): TelehealthApplicantRegistrationDetailsRepository

### Community 282 - "Community 282"
Cohesion: 0.31
Nodes (1): TelehealthApplicantRequestEligibilityPolicyTests

### Community 283 - "Community 283"
Cohesion: 0.31
Nodes (1): TelehealthApplicantRequestInsuranceSourcePolicyTests

### Community 284 - "Community 284"
Cohesion: 0.29
Nodes (1): TelehealthApplicantRequestIntakePolicyTests

### Community 285 - "Community 285"
Cohesion: 0.31
Nodes (1): TelehealthApplicantRequestLocationPolicyTests

### Community 286 - "Community 286"
Cohesion: 0.31
Nodes (1): TelehealthApplicantRequestPracticeNetworkPolicyTests

### Community 287 - "Community 287"
Cohesion: 0.36
Nodes (1): TelehealthApplicantRequestPracticeNetworkService

### Community 288 - "Community 288"
Cohesion: 0.47
Nodes (1): TelehealthApplicantRequestQueueStatusPolicyTests

### Community 289 - "Community 289"
Cohesion: 0.36
Nodes (1): TelehealthProspectiveIdentityProofingRepository

### Community 290 - "Community 290"
Cohesion: 0.40
Nodes (1): TelehealthProspectivePracticeNetworkPrecheckRepository

### Community 291 - "Community 291"
Cohesion: 0.36
Nodes (1): AddressBookRepository

### Community 294 - "Community 294"
Cohesion: 0.47
Nodes (8): facilities, inventory_cost_layer_applications, inventory_cost_layers, inventory_cost_policies, inventory_items, inventory_lots, inventory_purchase_receipts, inventory_transactions

### Community 295 - "Community 295"
Cohesion: 0.50
Nodes (8): patients, staff, therapy_group_members, therapy_group_session_attendance, therapy_group_session_encounters, therapy_group_session_participants, therapy_group_sessions, therapy_groups

### Community 296 - "Community 296"
Cohesion: 0.22
Nodes (6): emptyContactForm, getPatientPortalProfile(), PatientPortalProfileChangeInput, PatientPortalProfileDemographics, PatientPortalProfileResponse, submitPatientPortalProfileChange()

### Community 297 - "Community 297"
Cohesion: 0.28
Nodes (4): Get-EncounterDetail(), Get-HttpStatus(), Invoke-JsonRequest(), Invoke-StatusRequest()

### Community 298 - "Community 298"
Cohesion: 0.28
Nodes (3): Authorization-Path(), Get-AuthorizationStatus(), Invoke-Authorization()

### Community 299 - "Community 299"
Cohesion: 0.39
Nodes (5): Applicant-Headers(), Get-RequestCreation(), Get-RequestCreationStatus(), Invoke-RequestCreation(), Request-CreationPath()

### Community 300 - "Community 300"
Cohesion: 0.31
Nodes (4): Get-RequestLocation(), Get-RequestLocationStatus(), Invoke-RequestLocation(), Request-LocationPath()

### Community 301 - "Community 301"
Cohesion: 0.31
Nodes (4): Get-RequestSafety(), Get-RequestSafetyStatus(), Invoke-RequestSafety(), Request-SafetyPath()

### Community 302 - "Community 302"
Cohesion: 0.44
Nodes (2): ISyntheticTelehealthComplaintTriageEvaluator, SyntheticTelehealthComplaintTriageEvaluator

### Community 303 - "Community 303"
Cohesion: 0.39
Nodes (1): SyntheticTelehealthProspectiveIdentityProofingGatewayTests

### Community 304 - "Community 304"
Cohesion: 0.22
Nodes (1): SyntheticTelehealthProspectivePracticeNetworkCatalogTests

### Community 305 - "Community 305"
Cohesion: 0.39
Nodes (1): TelehealthApplicantIdentityReviewRepository

### Community 306 - "Community 306"
Cohesion: 0.42
Nodes (1): TelehealthApplicantIdentityReviewService

### Community 307 - "Community 307"
Cohesion: 0.44
Nodes (1): TelehealthApplicantNoticeRepository

### Community 308 - "Community 308"
Cohesion: 0.42
Nodes (1): TelehealthApplicantPromotionAuthorizationService

### Community 309 - "Community 309"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRegistrationDetailsPolicyTests

### Community 310 - "Community 310"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestOperationalReviewSubmissionPolicyTests

### Community 311 - "Community 311"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestUniversalSafetyPolicyTests

### Community 312 - "Community 312"
Cohesion: 0.42
Nodes (1): TelehealthApplicantSyntheticPromotionService

### Community 313 - "Community 313"
Cohesion: 0.33
Nodes (1): TelehealthProspectiveApplicantPolicyTests

### Community 314 - "Community 314"
Cohesion: 0.39
Nodes (1): TelehealthProspectivePracticeNetworkRepository

### Community 315 - "Community 315"
Cohesion: 0.36
Nodes (1): TelehealthProspectivePracticeNetworkService

### Community 316 - "Community 316"
Cohesion: 0.36
Nodes (1): TelehealthProspectiveSafetyTriagePolicyTests

### Community 317 - "Community 317"
Cohesion: 0.25
Nodes (8): critical_lab_result_acknowledgement_events, critical_lab_result_acknowledgements, lab_orders, lab_report_review_events, lab_reports, lab_results, lab_specimens, procedure_result_versions

### Community 318 - "Community 318"
Cohesion: 0.29
Nodes (1): RuntimeSafetyPolicy

### Community 319 - "Community 319"
Cohesion: 0.43
Nodes (1): OfficeNoteRepository

### Community 321 - "Community 321"
Cohesion: 0.39
Nodes (1): RecallRepository

### Community 322 - "Community 322"
Cohesion: 0.50
Nodes (7): facilities, inventory_cost_layers, inventory_cost_policies, inventory_items, inventory_lots, inventory_valuation_run_lines, inventory_valuation_runs

### Community 323 - "Community 323"
Cohesion: 0.57
Nodes (7): clinical_form_definition_events, clinical_form_definitions, clinical_form_instance_events, clinical_form_instances, clinical_form_revisions, clinical_form_signatures, patients

### Community 324 - "Community 324"
Cohesion: 0.50
Nodes (7): encounters, staff, telehealth_consultation_contexts, telehealth_consultation_disposition_draft_events, telehealth_consultation_disposition_draft_versions, trg_telehealth_disposition_events_append_only, trg_telehealth_disposition_versions_append_only

### Community 325 - "Community 325"
Cohesion: 0.25
Nodes (7): assetsRoot, distRoot, files, initial, initialMatch, result, violations

### Community 326 - "Community 326"
Cohesion: 0.29
Nodes (2): Get-CanonicalCounts(), Invoke-Scalar()

### Community 327 - "Community 327"
Cohesion: 0.39
Nodes (1): SyntheticTelehealthCoverageGatewayTests

### Community 328 - "Community 328"
Cohesion: 0.25
Nodes (1): SyntheticTelehealthPharmacyDirectoryTests

### Community 329 - "Community 329"
Cohesion: 0.43
Nodes (1): SyntheticTelehealthProspectiveEligibilityGatewayTests

### Community 330 - "Community 330"
Cohesion: 0.43
Nodes (1): SyntheticTelehealthProspectivePracticeNetworkGatewayTests

### Community 331 - "Community 331"
Cohesion: 0.32
Nodes (2): SyntheticTelehealthApplicantAllergyCatalog, TelehealthApplicantAllergyInformationPolicy

### Community 332 - "Community 332"
Cohesion: 0.32
Nodes (2): SyntheticTelehealthApplicantHealthHistoryTopicCatalog, TelehealthApplicantHealthHistoryInformationPolicy

### Community 333 - "Community 333"
Cohesion: 0.32
Nodes (2): SyntheticTelehealthApplicantMedicationCatalog, TelehealthApplicantMedicationInformationPolicy

### Community 334 - "Community 334"
Cohesion: 0.50
Nodes (1): TelehealthApplicantPracticeReviewClaimRepository

### Community 335 - "Community 335"
Cohesion: 0.39
Nodes (1): TelehealthApplicantPracticeReviewPacketPolicyTests

### Community 336 - "Community 336"
Cohesion: 0.25
Nodes (1): TelehealthApplicantSyntheticPromotionPolicyTests

### Community 337 - "Community 337"
Cohesion: 0.39
Nodes (1): TelehealthOpenApi

### Community 338 - "Community 338"
Cohesion: 0.43
Nodes (1): TelehealthPrescriptionService

### Community 339 - "Community 339"
Cohesion: 0.43
Nodes (1): TelehealthProspectiveEligibilityRepository

### Community 340 - "Community 340"
Cohesion: 0.36
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsPolicy

### Community 341 - "Community 341"
Cohesion: 0.43
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsRepository

### Community 342 - "Community 342"
Cohesion: 0.46
Nodes (1): ClinicalWorkflowPolicyCatalog

### Community 343 - "Community 343"
Cohesion: 0.57
Nodes (1): DatabaseBootstrapCatalogTests

### Community 344 - "Community 344"
Cohesion: 0.38
Nodes (4): f4268e0 feat(clinical): add medication lifecycle restore history, f858e05 feat(clinical): add medication content edit history, medication_list_lifecycle_events, medications

### Community 345 - "Community 345"
Cohesion: 0.48
Nodes (1): ChartTrackerRepository

### Community 347 - "Community 347"
Cohesion: 0.29
Nodes (4): IHealthCheck, PostgresReadinessHealthCheck, SchemaMigrationReadinessHealthCheck, TelehealthReadinessHealthCheck

### Community 348 - "Community 348"
Cohesion: 0.52
Nodes (6): facilities, inventory_items, inventory_purchase_requisition_events, inventory_purchase_requisition_lines, inventory_purchase_requisitions, inventory_vendors

### Community 349 - "Community 349"
Cohesion: 0.57
Nodes (6): inventory_controlled_count_discrepancies, inventory_controlled_count_lines, inventory_controlled_count_sessions, inventory_controlled_custody_events, inventory_controlled_locations, inventory_lots

### Community 350 - "Community 350"
Cohesion: 0.62
Nodes (6): facilities, inventory_items, inventory_replenishment_policies, inventory_replenishment_policy_change_request_events, inventory_replenishment_policy_change_requests, inventory_vendors

### Community 351 - "Community 351"
Cohesion: 0.48
Nodes (5): external_laboratory_source_facility_events, external_laboratory_source_facility_grants, external_laboratory_sources, facilities, trg_external_laboratory_source_facility_events_immutable

### Community 352 - "Community 352"
Cohesion: 0.33
Nodes (2): Get-PathOperation(), Get-PropertyValue()

### Community 353 - "Community 353"
Cohesion: 0.38
Nodes (3): Claim-Path(), Get-ClaimStatus(), Invoke-Claim()

### Community 354 - "Community 354"
Cohesion: 0.33
Nodes (2): Eligibility-Path(), Invoke-ContendedEligibilityPosts()

### Community 355 - "Community 355"
Cohesion: 0.33
Nodes (2): Invoke-ContendedSourcePosts(), Source-Path()

### Community 356 - "Community 356"
Cohesion: 0.33
Nodes (2): Intake-Path(), Invoke-ContendedIntakePosts()

### Community 357 - "Community 357"
Cohesion: 0.33
Nodes (2): Invoke-ContendedSubmissionPosts(), Submission-Path()

### Community 358 - "Community 358"
Cohesion: 0.33
Nodes (2): Invoke-ContendedParticipationPosts(), Participation-Path()

### Community 359 - "Community 359"
Cohesion: 0.33
Nodes (2): Evaluation-Path(), Invoke-ContendedEvaluationPosts()

### Community 360 - "Community 360"
Cohesion: 0.33
Nodes (2): Invoke-ContendedPracticeNetworkPosts(), PracticeNetwork-Path()

### Community 361 - "Community 361"
Cohesion: 0.33
Nodes (2): Candidate-Path(), Invoke-ContendedCandidatePosts()

### Community 362 - "Community 362"
Cohesion: 0.33
Nodes (3): ITelehealthCoverageGateway, SyntheticTelehealthAcknowledgment, SyntheticTelehealthCoverageGateway

### Community 363 - "Community 363"
Cohesion: 0.48
Nodes (1): SyntheticTelehealthProspectivePracticeNetworkCatalog

### Community 364 - "Community 364"
Cohesion: 0.29
Nodes (1): TelehealthApplicantIdentityReviewPolicyTests

### Community 365 - "Community 365"
Cohesion: 0.29
Nodes (1): TelehealthApplicantPracticeReviewInboxPolicyTests

### Community 366 - "Community 366"
Cohesion: 0.57
Nodes (1): TelehealthApplicantPracticeReviewSubmissionService

### Community 367 - "Community 367"
Cohesion: 0.29
Nodes (1): TelehealthApplicantPromotionAuthorizationPolicyTests

### Community 368 - "Community 368"
Cohesion: 0.62
Nodes (1): TelehealthProspectiveApplicantService

### Community 369 - "Community 369"
Cohesion: 0.43
Nodes (1): TelehealthProspectiveIdentityProofingService

### Community 370 - "Community 370"
Cohesion: 0.48
Nodes (2): ITelehealthProspectivePracticeNetworkGateway, SyntheticTelehealthProspectivePracticeNetworkGateway

### Community 371 - "Community 371"
Cohesion: 0.48
Nodes (1): TelehealthProspectivePracticeNetworkPrecheckService

### Community 372 - "Community 372"
Cohesion: 0.48
Nodes (1): TelehealthProspectiveSafetyTriageRepository

### Community 373 - "Community 373"
Cohesion: 0.48
Nodes (1): TelehealthProspectiveVisitPurposeRepository

### Community 374 - "Community 374"
Cohesion: 0.33
Nodes (2): getPatientMessages(), PatientMessagesResponse

### Community 375 - "Community 375"
Cohesion: 0.67
Nodes (1): PatientRecordRequestRepository

### Community 376 - "Community 376"
Cohesion: 0.67
Nodes (1): AzureOperationsEndpoints

### Community 377 - "Community 377"
Cohesion: 0.67
Nodes (5): encounter_track_reading_values, encounter_track_readings, encounter_track_records, encounters, track_anything_types

### Community 378 - "Community 378"
Cohesion: 0.60
Nodes (5): encounters, inventory_lots, inventory_patient_sales, inventory_transactions, patients

### Community 379 - "Community 379"
Cohesion: 0.53
Nodes (5): facilities, inventory_controlled_item_classification_events, inventory_controlled_location_events, inventory_controlled_locations, inventory_items

### Community 380 - "Community 380"
Cohesion: 0.67
Nodes (5): encounters, inventory_controlled_custody_events, inventory_controlled_locations, inventory_lots, patients

### Community 381 - "Community 381"
Cohesion: 0.67
Nodes (5): patient_disclosure_authorities, patient_disclosure_authority_events, patient_disclosure_request_events, patient_disclosure_requests, patients

### Community 382 - "Community 382"
Cohesion: 0.60
Nodes (5): auth_accounts, facilities, practice_setting_delegation_events, practice_setting_delegations, practice_settings

### Community 383 - "Community 383"
Cohesion: 0.33
Nodes (5): operations.audit_events, operations.operator_credentials, operations.runtime_state, operations.sessions, operations.usage_events

### Community 384 - "Community 384"
Cohesion: 0.47
Nodes (5): auth_sessions, azure_operations_access_audit, azure_operations_access_config, azure_operations_access_grants, azure_operations_unlock_attempts

### Community 385 - "Community 385"
Cohesion: 0.67
Nodes (5): auth_access_context_grant_events, auth_accounts, auth_principal_facility_grants, auth_principal_purpose_of_use_grants, facilities

### Community 386 - "Community 386"
Cohesion: 0.47
Nodes (4): prescription_audit_events, prescriptions, trg_prescription_audit_events_immutable, trg_prescriptions_retained

### Community 387 - "Community 387"
Cohesion: 0.53
Nodes (5): avenchart_require_active_patient_for_prescription_continuation(), patient_record, patients, prescriptions, trg_prescriptions_require_active_patient_for_continuation

### Community 388 - "Community 388"
Cohesion: 0.53
Nodes (4): auth_accounts, auth_external_identity_mapping_events, auth_external_identity_mappings, trg_auth_external_identity_mapping_events_immutable

### Community 389 - "Community 389"
Cohesion: 0.53
Nodes (4): patient_portal_external_identity_mapping_events, patient_portal_external_identity_mappings, patients, trg_patient_portal_external_identity_mapping_events_immutable

### Community 390 - "Community 390"
Cohesion: 0.53
Nodes (4): critical_lab_result_follow_up_events, critical_lab_result_follow_ups, lab_results, trg_critical_follow_up_events_append_only

### Community 391 - "Community 391"
Cohesion: 0.40
Nodes (2): Invoke-Api(), Start-TestApi()

### Community 392 - "Community 392"
Cohesion: 0.40
Nodes (2): Get-EncounterDetail(), Invoke-JsonRequest()

### Community 393 - "Community 393"
Cohesion: 0.47
Nodes (3): Get-Packet(), Get-PacketStatus(), Packet-Path()

### Community 394 - "Community 394"
Cohesion: 0.53
Nodes (1): TestIdentityProviderService

### Community 395 - "Community 395"
Cohesion: 0.47
Nodes (2): IStaffIdentityAdapter, LocalDevelopmentStaffIdentityAdapter

### Community 396 - "Community 396"
Cohesion: 0.60
Nodes (1): TelehealthApplicantAllergyInformationService

### Community 397 - "Community 397"
Cohesion: 0.47
Nodes (1): TelehealthApplicantClinicalInformationInventoryPolicy

### Community 398 - "Community 398"
Cohesion: 0.60
Nodes (1): TelehealthApplicantClinicalInformationInventoryService

### Community 399 - "Community 399"
Cohesion: 0.60
Nodes (1): TelehealthApplicantClinicalInformationSummaryService

### Community 400 - "Community 400"
Cohesion: 0.60
Nodes (1): TelehealthApplicantCommunicationAccessService

### Community 401 - "Community 401"
Cohesion: 0.60
Nodes (1): TelehealthApplicantDevicePreparationService

### Community 402 - "Community 402"
Cohesion: 0.60
Nodes (1): TelehealthApplicantHealthHistoryInformationService

### Community 403 - "Community 403"
Cohesion: 0.60
Nodes (1): TelehealthApplicantInsuranceHandoffService

### Community 404 - "Community 404"
Cohesion: 0.60
Nodes (1): TelehealthApplicantMedicationInformationService

### Community 405 - "Community 405"
Cohesion: 0.60
Nodes (1): TelehealthApplicantNoticeService

### Community 406 - "Community 406"
Cohesion: 0.33
Nodes (1): TelehealthApplicantPracticeReviewAuthorizationPolicyTests

### Community 407 - "Community 407"
Cohesion: 0.53
Nodes (1): TelehealthApplicantPracticeReviewAuthorizationRepository

### Community 408 - "Community 408"
Cohesion: 0.33
Nodes (1): TelehealthApplicantPreRequestReadinessPolicy

### Community 409 - "Community 409"
Cohesion: 0.60
Nodes (1): TelehealthApplicantPreRequestReadinessService

### Community 410 - "Community 410"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRegistrationDetailsService

### Community 411 - "Community 411"
Cohesion: 0.33
Nodes (1): TelehealthApplicantRequestCreationPolicyTests

### Community 412 - "Community 412"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestCreationService

### Community 413 - "Community 413"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestInsuranceSourceService

### Community 414 - "Community 414"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestIntakeService

### Community 415 - "Community 415"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestLocationService

### Community 416 - "Community 416"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestOperationalReviewSubmissionService

### Community 417 - "Community 417"
Cohesion: 0.47
Nodes (1): TelehealthApplicantRequestParticipationContextPolicy

### Community 418 - "Community 418"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestParticipationContextService

### Community 419 - "Community 419"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestParticipationEvaluationService

### Community 420 - "Community 420"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestQueueAuthorizationService

### Community 421 - "Community 421"
Cohesion: 0.47
Nodes (1): TelehealthApplicantRequestRenderingCandidatePolicy

### Community 422 - "Community 422"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestRenderingCandidateService

### Community 423 - "Community 423"
Cohesion: 0.60
Nodes (1): TelehealthApplicantRequestUniversalSafetyService

### Community 424 - "Community 424"
Cohesion: 0.33
Nodes (1): TelehealthApplicantSyntheticPromotionPolicy

### Community 425 - "Community 425"
Cohesion: 0.40
Nodes (2): TelehealthRuntimeSafetyPolicy, TelehealthServiceRegistration

### Community 426 - "Community 426"
Cohesion: 0.47
Nodes (2): ITelehealthProspectiveEligibilityGateway, SyntheticTelehealthProspectiveEligibilityGateway

### Community 427 - "Community 427"
Cohesion: 0.47
Nodes (2): ITelehealthProspectiveIdentityProofingGateway, SyntheticTelehealthProspectiveIdentityProofingGateway

### Community 428 - "Community 428"
Cohesion: 0.33
Nodes (1): TelehealthProspectiveSafetyTriagePolicy

### Community 429 - "Community 429"
Cohesion: 0.33
Nodes (1): TelehealthProspectiveVisitPurposePolicyTests

### Community 430 - "Community 430"
Cohesion: 0.47
Nodes (2): ITelehealthVideoProvider, SyntheticTelehealthVideoProvider

### Community 431 - "Community 431"
Cohesion: 0.40
Nodes (5): inventory_items, inventory_lots, inventory_purchase_receipts, inventory_transactions, inventory_vendors

### Community 432 - "Community 432"
Cohesion: 0.60
Nodes (1): DevelopmentTestIdentityProviderEndpoints

### Community 433 - "Community 433"
Cohesion: 0.80
Nodes (1): FhirR4ValidationService

### Community 434 - "Community 434"
Cohesion: 0.60
Nodes (1): PatientEndpoints

### Community 435 - "Community 435"
Cohesion: 0.70
Nodes (4): facilities, inventory_items, inventory_lots, inventory_transactions

### Community 436 - "Community 436"
Cohesion: 0.70
Nodes (4): patient_merge_audit_plans, patient_merge_execution_manifest_rows, patient_merge_executions, patients

### Community 437 - "Community 437"
Cohesion: 0.70
Nodes (4): facilities, patients, recalls, staff

### Community 438 - "Community 438"
Cohesion: 0.70
Nodes (4): chart_tracker_events, chart_tracker_locations, patients, staff

### Community 439 - "Community 439"
Cohesion: 0.70
Nodes (4): encounters, inventory_items, inventory_patient_sale_batches, patients

### Community 440 - "Community 440"
Cohesion: 0.80
Nodes (4): inventory_item_medication_link_audits, inventory_item_medication_links, inventory_items, medication_vocabulary

### Community 441 - "Community 441"
Cohesion: 0.70
Nodes (4): inventory_purchase_receipts, inventory_purchase_requisition_lines, inventory_purchase_requisition_receipts, inventory_purchase_requisitions

### Community 442 - "Community 442"
Cohesion: 0.70
Nodes (4): inventory_lot_destructions, inventory_lot_expiry_dispositions, inventory_lots, inventory_transactions

### Community 443 - "Community 443"
Cohesion: 0.70
Nodes (4): authorizations, clinical_workflow_events, patients, referrals

### Community 444 - "Community 444"
Cohesion: 0.90
Nodes (4): saved_report_definition_events, saved_report_definition_revisions, saved_report_definitions, saved_report_runs

### Community 445 - "Community 445"
Cohesion: 0.70
Nodes (4): azure_deployment_execution_events, azure_deployment_executions, azure_deployment_profile_revisions, azure_deployment_profiles

### Community 446 - "Community 446"
Cohesion: 0.60
Nodes (4): lab_orders, lab_results, procedure_order_events, procedure_result_events

### Community 447 - "Community 447"
Cohesion: 0.60
Nodes (3): integration_outbox, integration_outbox_provenance_events, trg_integration_outbox_provenance_events_immutable

### Community 448 - "Community 448"
Cohesion: 0.50
Nodes (2): Invoke-FixtureSql(), Set-FixturePortalState()

### Community 450 - "Community 450"
Cohesion: 0.50
Nodes (1): AuthorizationPolicyCatalog

### Community 451 - "Community 451"
Cohesion: 0.50
Nodes (1): TelehealthApplicantInsuranceHandoffPolicy

### Community 452 - "Community 452"
Cohesion: 0.60
Nodes (1): TelehealthApplicantPracticeReviewInboxService

### Community 453 - "Community 453"
Cohesion: 0.80
Nodes (1): TelehealthApplicantRequestComplaintTriageService

### Community 454 - "Community 454"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestIntakePolicy

### Community 455 - "Community 455"
Cohesion: 0.40
Nodes (1): TelehealthApplicantRequestParticipationEvaluationPolicy

### Community 456 - "Community 456"
Cohesion: 0.70
Nodes (1): TelehealthApplicantRequestQueueStatusRepository

### Community 457 - "Community 457"
Cohesion: 0.40
Nodes (1): TelehealthAuthorizationTests

### Community 458 - "Community 458"
Cohesion: 0.70
Nodes (1): TelehealthPatientQueueStatusProjectorTests

### Community 459 - "Community 459"
Cohesion: 0.50
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsProtector

### Community 460 - "Community 460"
Cohesion: 0.60
Nodes (1): TelehealthProspectiveMemberInsuranceDetailsService

### Community 461 - "Community 461"
Cohesion: 0.60
Nodes (1): TelehealthProspectiveSafetyTriageService

### Community 462 - "Community 462"
Cohesion: 0.60
Nodes (1): TelehealthProspectiveVisitPurposeService

### Community 463 - "Community 463"
Cohesion: 0.50
Nodes (1): StaffAccessContextServiceTests

### Community 464 - "Community 464"
Cohesion: 0.67
Nodes (4): access_group_permissions, access_groups, access_permissions, access_user_memberships

### Community 465 - "Community 465"
Cohesion: 0.50
Nodes (4): patient_disclosure_authorities, patient_disclosure_authority_events, patient_disclosure_request_events, patient_disclosure_requests

### Community 466 - "Community 466"
Cohesion: 0.50
Nodes (3): 1b2ad1b docs(phase-2): record Graphify index evidence, c3a55ee chore(tooling): add Graphify code-navigation index, ccb790f docs(phase-2): record Graphify supply-chain residual

### Community 467 - "Community 467"
Cohesion: 0.67
Nodes (1): ExternalLaboratoryFhirIntakeEndpoints

### Community 468 - "Community 468"
Cohesion: 0.83
Nodes (1): FhirR4Endpoints

### Community 469 - "Community 469"
Cohesion: 0.67
Nodes (1): IntegrationEndpoints

### Community 470 - "Community 470"
Cohesion: 0.83
Nodes (3): form_layout_fields, form_layout_groups, form_layouts

### Community 471 - "Community 471"
Cohesion: 0.83
Nodes (3): encounter_layout_form_records, encounter_layout_form_values, form_layouts

### Community 472 - "Community 472"
Cohesion: 0.83
Nodes (3): batch_communication_campaigns, batch_communication_recipients, patients

### Community 473 - "Community 473"
Cohesion: 0.83
Nodes (3): facilities, inventory_purchase_receipts, inventory_vendors

### Community 474 - "Community 474"
Cohesion: 0.83
Nodes (3): practice_setting_change_request_events, practice_setting_change_requests, practice_settings

### Community 475 - "Community 475"
Cohesion: 0.83
Nodes (3): document_template_binary_versions, document_template_events, document_templates

### Community 476 - "Community 476"
Cohesion: 0.83
Nodes (3): inventory_cost_policies, inventory_cost_policy_change_request_events, inventory_cost_policy_change_requests

### Community 477 - "Community 477"
Cohesion: 0.83
Nodes (3): inventory_accounting_integration_change_request_events, inventory_accounting_integration_change_requests, inventory_accounting_integration_decisions

### Community 478 - "Community 478"
Cohesion: 0.83
Nodes (3): message_assignment_events, messages, patients

### Community 479 - "Community 479"
Cohesion: 0.83
Nodes (3): messages, patients, staff_message_attachments

### Community 480 - "Community 480"
Cohesion: 0.83
Nodes (3): message_correction_events, messages, patients

### Community 481 - "Community 481"
Cohesion: 0.83
Nodes (3): message_retention_events, messages, patients

### Community 482 - "Community 482"
Cohesion: 0.83
Nodes (3): integration_idempotency_conflicts, integration_inbox, integration_outbox

### Community 486 - "Community 486"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestEligibilityPolicy

### Community 487 - "Community 487"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestInsuranceSourcePolicy

### Community 488 - "Community 488"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestLocationPolicy

### Community 489 - "Community 489"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestOperationalReviewSubmissionPolicy

### Community 490 - "Community 490"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestPracticeNetworkPolicy

### Community 491 - "Community 491"
Cohesion: 0.50
Nodes (1): TelehealthApplicantRequestQueueAuthorizationPolicy

### Community 492 - "Community 492"
Cohesion: 0.50
Nodes (1): TelehealthAuthorizationPolicy

### Community 493 - "Community 493"
Cohesion: 0.67
Nodes (1): TelehealthPatientQueueStatusProjector

### Community 494 - "Community 494"
Cohesion: 0.67
Nodes (1): AdministrationEndpoints

### Community 495 - "Community 495"
Cohesion: 0.67
Nodes (1): AdministrativeReferenceEndpoints

### Community 496 - "Community 496"
Cohesion: 0.67
Nodes (1): AppointmentEndpoints

### Community 497 - "Community 497"
Cohesion: 0.67
Nodes (1): BillingEndpoints

### Community 498 - "Community 498"
Cohesion: 0.67
Nodes (1): ClinicalFormEndpoints

### Community 499 - "Community 499"
Cohesion: 0.67
Nodes (1): ClinicalListEndpoints

### Community 500 - "Community 500"
Cohesion: 0.67
Nodes (1): ClinicalWorkflowEndpoints

### Community 501 - "Community 501"
Cohesion: 0.67
Nodes (1): ConfigurationEndpoints

### Community 502 - "Community 502"
Cohesion: 0.67
Nodes (1): DocumentEndpoints

### Community 503 - "Community 503"
Cohesion: 0.67
Nodes (1): DocumentTemplateEndpoints

### Community 504 - "Community 504"
Cohesion: 0.67
Nodes (1): EncounterEndpoints

### Community 505 - "Community 505"
Cohesion: 0.67
Nodes (1): InventoryEndpoints

### Community 506 - "Community 506"
Cohesion: 0.67
Nodes (1): ManagedRecordEndpoints

### Community 507 - "Community 507"
Cohesion: 0.67
Nodes (1): MessageEndpoints

### Community 508 - "Community 508"
Cohesion: 0.67
Nodes (1): OfficeNoteEndpoints

### Community 509 - "Community 509"
Cohesion: 0.67
Nodes (1): PatientEngagementEndpoints

### Community 510 - "Community 510"
Cohesion: 0.67
Nodes (1): PatientPortalEndpoints

### Community 511 - "Community 511"
Cohesion: 0.67
Nodes (1): ProcedureEndpoints

### Community 512 - "Community 512"
Cohesion: 0.67
Nodes (1): ReportEndpoints

### Community 513 - "Community 513"
Cohesion: 0.67
Nodes (1): TherapyGroupEndpoints

### Community 514 - "Community 514"
Cohesion: 0.67
Nodes (2): integration_inbox, integration_outbox

### Community 515 - "Community 515"
Cohesion: 1.00
Nodes (2): coding_catalog_audit_events, coding_catalogs

### Community 516 - "Community 516"
Cohesion: 1.00
Nodes (2): form_option_lists, form_option_values

### Community 517 - "Community 517"
Cohesion: 1.00
Nodes (2): clinical_alert_rules, encounter_clinical_alert_acknowledgments

### Community 518 - "Community 518"
Cohesion: 1.00
Nodes (2): patient_record_requests, patients

### Community 519 - "Community 519"
Cohesion: 1.00
Nodes (2): patient_sdoh_assessments, patients

### Community 520 - "Community 520"
Cohesion: 1.00
Nodes (2): recall_activity, recalls

### Community 521 - "Community 521"
Cohesion: 1.00
Nodes (2): patient_duplicate_review_dispositions, patients

### Community 522 - "Community 522"
Cohesion: 1.00
Nodes (2): document_template_binary_versions, document_templates

### Community 523 - "Community 523"
Cohesion: 1.00
Nodes (2): patient_xml_exchange_audits, patients

### Community 524 - "Community 524"
Cohesion: 1.00
Nodes (2): inventory_count_reconciliations, inventory_lots

### Community 525 - "Community 525"
Cohesion: 1.33
Nodes (2): practice_setting_revisions, practice_settings

### Community 526 - "Community 526"
Cohesion: 1.33
Nodes (2): coding_catalog_revisions, coding_catalogs

### Community 527 - "Community 527"
Cohesion: 1.33
Nodes (2): form_option_list_revisions, form_option_lists

### Community 528 - "Community 528"
Cohesion: 1.33
Nodes (2): form_layout_revisions, form_layouts

### Community 529 - "Community 529"
Cohesion: 1.33
Nodes (2): clinical_alert_rule_revisions, clinical_alert_rules

### Community 530 - "Community 530"
Cohesion: 1.33
Nodes (2): module_catalog, module_catalog_revisions

### Community 531 - "Community 531"
Cohesion: 1.33
Nodes (2): api_client_registry, api_client_registry_revisions

### Community 532 - "Community 532"
Cohesion: 1.00
Nodes (2): inventory_lot_metadata_audits, inventory_lots

### Community 533 - "Community 533"
Cohesion: 1.00
Nodes (2): inventory_lot_destructions, inventory_lots

### Community 534 - "Community 534"
Cohesion: 1.00
Nodes (2): inventory_controlled_locations, inventory_controlled_report_runs

### Community 535 - "Community 535"
Cohesion: 1.00
Nodes (2): inventory_controlled_report_exports, inventory_controlled_report_runs

### Community 536 - "Community 536"
Cohesion: 1.00
Nodes (2): coding_catalog_change_request_events, coding_catalog_change_requests

### Community 537 - "Community 537"
Cohesion: 1.00
Nodes (2): form_layout_change_request_events, form_layout_change_requests

### Community 538 - "Community 538"
Cohesion: 1.00
Nodes (2): form_option_list_change_request_events, form_option_list_change_requests

### Community 539 - "Community 539"
Cohesion: 1.00
Nodes (2): clinical_alert_rule_change_request_events, clinical_alert_rule_change_requests

### Community 540 - "Community 540"
Cohesion: 1.00
Nodes (2): module_change_request_events, module_change_requests

### Community 541 - "Community 541"
Cohesion: 1.00
Nodes (2): api_client_change_request_events, api_client_change_requests

### Community 542 - "Community 542"
Cohesion: 1.00
Nodes (2): lab_specimens, procedure_specimen_events

### Community 543 - "Community 543"
Cohesion: 1.00
Nodes (2): recall_lifecycle_events, recalls

### Community 544 - "Community 544"
Cohesion: 1.00
Nodes (2): lab_reports, lab_specimens

### Community 545 - "Community 545"
Cohesion: 1.00
Nodes (2): patient_registration_duplicate_reviews, patients

### Community 548 - "Community 548"
Cohesion: 0.67
Nodes (1): TelehealthApplicantConnectionPolicy

### Community 549 - "Community 549"
Cohesion: 0.67
Nodes (1): TelehealthApplicantConnectionPolicyTests

### Community 550 - "Community 550"
Cohesion: 0.67
Nodes (1): TelehealthApplicantRequestCreationPolicy

### Community 551 - "Community 551"
Cohesion: 0.67
Nodes (1): TelehealthApplicantRequestQueueStatusPolicy

### Community 552 - "Community 552"
Cohesion: 1.00
Nodes (1): TelehealthApplicantRequestQueueStatusService

### Community 553 - "Community 553"
Cohesion: 0.67
Nodes (1): TelehealthStateMachineTests

### Community 554 - "Community 554"
Cohesion: 1.00
Nodes (2): pharmacies, prescriptions

### Community 555 - "Community 555"
Cohesion: 1.00
Nodes (1): AzureOperationsOptions

### Community 556 - "Community 556"
Cohesion: 1.00
Nodes (1): DatabaseConnectionOptions

### Community 557 - "Community 557"
Cohesion: 1.00
Nodes (1): schema_migrations

### Community 558 - "Community 558"
Cohesion: 1.00
Nodes (1): statement_email_outbox

### Community 559 - "Community 559"
Cohesion: 1.00
Nodes (1): phi_access_audit_events

### Community 560 - "Community 560"
Cohesion: 1.00
Nodes (1): encounter_audit_events

### Community 561 - "Community 561"
Cohesion: 1.00
Nodes (1): clinical_alert_rules

### Community 562 - "Community 562"
Cohesion: 1.00
Nodes (1): module_catalog

### Community 563 - "Community 563"
Cohesion: 1.00
Nodes (1): api_client_registry

### Community 564 - "Community 564"
Cohesion: 1.00
Nodes (1): office_notes

### Community 565 - "Community 565"
Cohesion: 1.00
Nodes (1): address_book_contacts

### Community 566 - "Community 566"
Cohesion: 2.00
Nodes (1): track_anything_types

### Community 567 - "Community 567"
Cohesion: 1.00
Nodes (1): patient_education_resources

### Community 568 - "Community 568"
Cohesion: 1.00
Nodes (1): document_templates

### Community 569 - "Community 569"
Cohesion: 1.00
Nodes (1): inventory_controlled_action_attestations

### Community 570 - "Community 570"
Cohesion: 1.00
Nodes (1): AvenChart.Api.csproj

## Knowledge Gaps
- **994 isolated node(s):** `AccessibilityFinding`, `clinicianFixture`, `codingEncounter`, `encounter`, `composeRoot` (+989 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Community 11`** (1 nodes): `TelehealthEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 15`** (1 nodes): `BillingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 16`** (1 nodes): `DocumentRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 19`** (1 nodes): `PatientRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 20`** (1 nodes): `AppointmentRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 21`** (1 nodes): `ProcedureRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 26`** (2 nodes): `DiagnosisAccumulator`, `EncounterRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 27`** (1 nodes): `PatientPortalRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 28`** (1 nodes): `ClinicalFormRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 31`** (1 nodes): `AdministrationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 32`** (1 nodes): `ClinicalFormRuntime`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 42`** (1 nodes): `TelehealthRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 43`** (1 nodes): `ReportExecutionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 46`** (1 nodes): `ClinicalListRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 53`** (1 nodes): `MessageRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 59`** (2 nodes): `InventoryItemBuilder`, `InventoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 64`** (1 nodes): `ManagedRecordRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 67`** (1 nodes): `ReportRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 70`** (1 nodes): `BrowserOidcSessionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 71`** (1 nodes): `ReportDefinitionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 74`** (1 nodes): `TelehealthConsultationServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 87`** (1 nodes): `AuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 88`** (1 nodes): `IntegrationIdempotencyConflictException`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 89`** (1 nodes): `PatientDisclosureRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 93`** (1 nodes): `AzureOperationsRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 94`** (1 nodes): `ClinicalListStateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 95`** (1 nodes): `FhirRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 97`** (1 nodes): `AzureDeploymentProfilePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 102`** (1 nodes): `ReferralRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 105`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 106`** (1 nodes): `TelehealthService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 109`** (1 nodes): `DocumentTemplateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 110`** (1 nodes): `PatientMergeExecutionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 111`** (2 nodes): `ReportExecutionQueueRepository`, `WorkerCancellationState`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 113`** (1 nodes): `InventoryReplenishmentPolicyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 117`** (1 nodes): `TelehealthApplicantPreRequestReadinessRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 125`** (1 nodes): `TelehealthConsultationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 128`** (1 nodes): `AdministrationDirectoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 130`** (2 nodes): `AuthRepository`, `ToResponse()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 131`** (1 nodes): `InventoryCostPolicyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 133`** (1 nodes): `ProcedureDirectoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 138`** (1 nodes): `ExternalLaboratorySourceRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 139`** (1 nodes): `InventoryAccountingIntegrationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 143`** (1 nodes): `TelehealthApplicantRequestParticipationEvaluationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 146`** (1 nodes): `ToInventoryLot()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 147`** (1 nodes): `ToResponse()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 148`** (1 nodes): `TherapyGroupRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 153`** (1 nodes): `TelehealthApplicantAllergyInformationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 154`** (1 nodes): `TelehealthApplicantClinicalInformationSummaryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 155`** (1 nodes): `TelehealthApplicantCommunicationAccessRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 156`** (1 nodes): `TelehealthApplicantHealthHistoryInformationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 157`** (1 nodes): `TelehealthApplicantMedicationInformationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 158`** (1 nodes): `TelehealthApplicantRequestParticipationContextRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 159`** (1 nodes): `TelehealthApplicantRequestRenderingCandidateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 160`** (1 nodes): `TelehealthProspectiveApplicantRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 163`** (1 nodes): `EncounterStateRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 167`** (1 nodes): `TelehealthApplicantAllergyInformationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 168`** (1 nodes): `TelehealthApplicantClinicalInformationInventoryRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 169`** (1 nodes): `TelehealthApplicantDevicePreparationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 170`** (1 nodes): `TelehealthApplicantHealthHistoryInformationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 171`** (1 nodes): `TelehealthApplicantMedicationInformationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 172`** (1 nodes): `TelehealthApplicantRequestComplaintTriageRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 173`** (1 nodes): `TelehealthApplicantRequestEligibilityRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 174`** (1 nodes): `TelehealthApplicantRequestIntakeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 175`** (1 nodes): `TelehealthApplicantRequestPracticeNetworkRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 176`** (1 nodes): `TelehealthApplicantRequestUniversalSafetyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 177`** (1 nodes): `TelehealthConsultationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 179`** (1 nodes): `InventoryValuationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 182`** (1 nodes): `TelehealthApplicantInsuranceHandoffRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 183`** (1 nodes): `TelehealthApplicantRequestInsuranceSourceRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 184`** (1 nodes): `TelehealthApplicantRequestOperationalReviewSubmissionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 191`** (1 nodes): `PatientSdohRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 192`** (1 nodes): `AvenChartOpenApi`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 198`** (1 nodes): `TelehealthPrescriptionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 209`** (1 nodes): `TelehealthApplicantRequestLocationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 210`** (1 nodes): `TelehealthApplicantRequestQueueAuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 211`** (1 nodes): `TelehealthApplicantSyntheticPromotionRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 212`** (2 nodes): `TelehealthDispositionRepository`, `TelehealthSafetyDispositionConflictException`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 213`** (1 nodes): `TelehealthVideoRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 214`** (2 nodes): `0701dc1 Merge pull request #1 from nkimber/codex/local-docker-scripts`, `286a7d3 Add local Docker management scripts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 216`** (1 nodes): `AzureOperationsAccessRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 217`** (1 nodes): `BatchCommunicationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 218`** (1 nodes): `LegacyClinicalFormDisplayRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 219`** (1 nodes): `EndpointAccessPolicies`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 226`** (1 nodes): `TelehealthApplicantRequestComplaintTriagePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 227`** (1 nodes): `TelehealthApplicantRequestComplaintTriagePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 228`** (1 nodes): `TelehealthApplicantRequestEligibilityService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 229`** (2 nodes): `IPharmacyDirectory`, `SyntheticTelehealthPharmacyDirectory`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 230`** (1 nodes): `TelehealthPharmacyRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 231`** (1 nodes): `TelehealthPrescriptionServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 232`** (1 nodes): `TelehealthVideoService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 235`** (1 nodes): `ClinicalAlertEvaluationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 236`** (1 nodes): `EncounterLayoutFormRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 237`** (1 nodes): `ExternalIdentityMappingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 238`** (1 nodes): `PatientPortalExternalIdentityMappingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 239`** (1 nodes): `PatientPrintRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 240`** (1 nodes): `PatientXmlExchangeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 249`** (2 nodes): `Scalar()`, `Sql-Fails()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 251`** (1 nodes): `TelehealthApplicantDevicePreparationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 252`** (1 nodes): `TelehealthApplicantPracticeReviewSubmissionPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 253`** (1 nodes): `TelehealthApplicantPreRequestReadinessPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 254`** (1 nodes): `TelehealthApplicantRequestCreationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 255`** (1 nodes): `TelehealthApplicantRequestParticipationContextPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 256`** (1 nodes): `TelehealthApplicantRequestParticipationEvaluationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 257`** (1 nodes): `TelehealthApplicantRequestQueueAuthorizationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 258`** (1 nodes): `TelehealthApplicantRequestRenderingCandidatePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 259`** (1 nodes): `TelehealthApplicantRequestUniversalSafetyPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 260`** (1 nodes): `TelehealthProspectiveApplicantPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 261`** (1 nodes): `TelehealthProspectiveEligibilityService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 262`** (1 nodes): `TelehealthSafetyDispositionRulesTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 263`** (1 nodes): `FhirR4ValidationServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 266`** (1 nodes): `DatabaseSchemaMigrator`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 274`** (1 nodes): `SyntheticTelehealthComplaintTriageEvaluatorTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 275`** (1 nodes): `TelehealthApplicantClinicalInformationInventoryPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 276`** (1 nodes): `TelehealthApplicantClinicalInformationSummaryPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 277`** (1 nodes): `TelehealthApplicantCommunicationAccessPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 278`** (1 nodes): `TelehealthApplicantInsuranceHandoffPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 279`** (1 nodes): `TelehealthApplicantNoticePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 280`** (1 nodes): `TelehealthApplicantPromotionAuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 281`** (1 nodes): `TelehealthApplicantRegistrationDetailsRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 282`** (1 nodes): `TelehealthApplicantRequestEligibilityPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 283`** (1 nodes): `TelehealthApplicantRequestInsuranceSourcePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 284`** (1 nodes): `TelehealthApplicantRequestIntakePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 285`** (1 nodes): `TelehealthApplicantRequestLocationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 286`** (1 nodes): `TelehealthApplicantRequestPracticeNetworkPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 287`** (1 nodes): `TelehealthApplicantRequestPracticeNetworkService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 288`** (1 nodes): `TelehealthApplicantRequestQueueStatusPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 289`** (1 nodes): `TelehealthProspectiveIdentityProofingRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 290`** (1 nodes): `TelehealthProspectivePracticeNetworkPrecheckRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 291`** (1 nodes): `AddressBookRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 302`** (2 nodes): `ISyntheticTelehealthComplaintTriageEvaluator`, `SyntheticTelehealthComplaintTriageEvaluator`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 303`** (1 nodes): `SyntheticTelehealthProspectiveIdentityProofingGatewayTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 304`** (1 nodes): `SyntheticTelehealthProspectivePracticeNetworkCatalogTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 305`** (1 nodes): `TelehealthApplicantIdentityReviewRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 306`** (1 nodes): `TelehealthApplicantIdentityReviewService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 307`** (1 nodes): `TelehealthApplicantNoticeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 308`** (1 nodes): `TelehealthApplicantPromotionAuthorizationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 309`** (1 nodes): `TelehealthApplicantRegistrationDetailsPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 310`** (1 nodes): `TelehealthApplicantRequestOperationalReviewSubmissionPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 311`** (1 nodes): `TelehealthApplicantRequestUniversalSafetyPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 312`** (1 nodes): `TelehealthApplicantSyntheticPromotionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 313`** (1 nodes): `TelehealthProspectiveApplicantPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 314`** (1 nodes): `TelehealthProspectivePracticeNetworkRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 315`** (1 nodes): `TelehealthProspectivePracticeNetworkService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 316`** (1 nodes): `TelehealthProspectiveSafetyTriagePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 318`** (1 nodes): `RuntimeSafetyPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 319`** (1 nodes): `OfficeNoteRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 321`** (1 nodes): `RecallRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 326`** (2 nodes): `Get-CanonicalCounts()`, `Invoke-Scalar()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 327`** (1 nodes): `SyntheticTelehealthCoverageGatewayTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 328`** (1 nodes): `SyntheticTelehealthPharmacyDirectoryTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 329`** (1 nodes): `SyntheticTelehealthProspectiveEligibilityGatewayTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 330`** (1 nodes): `SyntheticTelehealthProspectivePracticeNetworkGatewayTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 331`** (2 nodes): `SyntheticTelehealthApplicantAllergyCatalog`, `TelehealthApplicantAllergyInformationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 332`** (2 nodes): `SyntheticTelehealthApplicantHealthHistoryTopicCatalog`, `TelehealthApplicantHealthHistoryInformationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 333`** (2 nodes): `SyntheticTelehealthApplicantMedicationCatalog`, `TelehealthApplicantMedicationInformationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 334`** (1 nodes): `TelehealthApplicantPracticeReviewClaimRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 335`** (1 nodes): `TelehealthApplicantPracticeReviewPacketPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 336`** (1 nodes): `TelehealthApplicantSyntheticPromotionPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 337`** (1 nodes): `TelehealthOpenApi`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 338`** (1 nodes): `TelehealthPrescriptionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 339`** (1 nodes): `TelehealthProspectiveEligibilityRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 340`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 341`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 342`** (1 nodes): `ClinicalWorkflowPolicyCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 343`** (1 nodes): `DatabaseBootstrapCatalogTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 345`** (1 nodes): `ChartTrackerRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 352`** (2 nodes): `Get-PathOperation()`, `Get-PropertyValue()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 354`** (2 nodes): `Eligibility-Path()`, `Invoke-ContendedEligibilityPosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 355`** (2 nodes): `Invoke-ContendedSourcePosts()`, `Source-Path()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 356`** (2 nodes): `Intake-Path()`, `Invoke-ContendedIntakePosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 357`** (2 nodes): `Invoke-ContendedSubmissionPosts()`, `Submission-Path()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 358`** (2 nodes): `Invoke-ContendedParticipationPosts()`, `Participation-Path()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 359`** (2 nodes): `Evaluation-Path()`, `Invoke-ContendedEvaluationPosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 360`** (2 nodes): `Invoke-ContendedPracticeNetworkPosts()`, `PracticeNetwork-Path()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 361`** (2 nodes): `Candidate-Path()`, `Invoke-ContendedCandidatePosts()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 363`** (1 nodes): `SyntheticTelehealthProspectivePracticeNetworkCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 364`** (1 nodes): `TelehealthApplicantIdentityReviewPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 365`** (1 nodes): `TelehealthApplicantPracticeReviewInboxPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 366`** (1 nodes): `TelehealthApplicantPracticeReviewSubmissionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 367`** (1 nodes): `TelehealthApplicantPromotionAuthorizationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 368`** (1 nodes): `TelehealthProspectiveApplicantService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 369`** (1 nodes): `TelehealthProspectiveIdentityProofingService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 370`** (2 nodes): `ITelehealthProspectivePracticeNetworkGateway`, `SyntheticTelehealthProspectivePracticeNetworkGateway`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 371`** (1 nodes): `TelehealthProspectivePracticeNetworkPrecheckService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 372`** (1 nodes): `TelehealthProspectiveSafetyTriageRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 373`** (1 nodes): `TelehealthProspectiveVisitPurposeRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 374`** (2 nodes): `getPatientMessages()`, `PatientMessagesResponse`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 375`** (1 nodes): `PatientRecordRequestRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 376`** (1 nodes): `AzureOperationsEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 391`** (2 nodes): `Invoke-Api()`, `Start-TestApi()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 392`** (2 nodes): `Get-EncounterDetail()`, `Invoke-JsonRequest()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 394`** (1 nodes): `TestIdentityProviderService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 395`** (2 nodes): `IStaffIdentityAdapter`, `LocalDevelopmentStaffIdentityAdapter`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 396`** (1 nodes): `TelehealthApplicantAllergyInformationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 397`** (1 nodes): `TelehealthApplicantClinicalInformationInventoryPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 398`** (1 nodes): `TelehealthApplicantClinicalInformationInventoryService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 399`** (1 nodes): `TelehealthApplicantClinicalInformationSummaryService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 400`** (1 nodes): `TelehealthApplicantCommunicationAccessService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 401`** (1 nodes): `TelehealthApplicantDevicePreparationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 402`** (1 nodes): `TelehealthApplicantHealthHistoryInformationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 403`** (1 nodes): `TelehealthApplicantInsuranceHandoffService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 404`** (1 nodes): `TelehealthApplicantMedicationInformationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 405`** (1 nodes): `TelehealthApplicantNoticeService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 406`** (1 nodes): `TelehealthApplicantPracticeReviewAuthorizationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 407`** (1 nodes): `TelehealthApplicantPracticeReviewAuthorizationRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 408`** (1 nodes): `TelehealthApplicantPreRequestReadinessPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 409`** (1 nodes): `TelehealthApplicantPreRequestReadinessService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 410`** (1 nodes): `TelehealthApplicantRegistrationDetailsService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 411`** (1 nodes): `TelehealthApplicantRequestCreationPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 412`** (1 nodes): `TelehealthApplicantRequestCreationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 413`** (1 nodes): `TelehealthApplicantRequestInsuranceSourceService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 414`** (1 nodes): `TelehealthApplicantRequestIntakeService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 415`** (1 nodes): `TelehealthApplicantRequestLocationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 416`** (1 nodes): `TelehealthApplicantRequestOperationalReviewSubmissionService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 417`** (1 nodes): `TelehealthApplicantRequestParticipationContextPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 418`** (1 nodes): `TelehealthApplicantRequestParticipationContextService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 419`** (1 nodes): `TelehealthApplicantRequestParticipationEvaluationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 420`** (1 nodes): `TelehealthApplicantRequestQueueAuthorizationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 421`** (1 nodes): `TelehealthApplicantRequestRenderingCandidatePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 422`** (1 nodes): `TelehealthApplicantRequestRenderingCandidateService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 423`** (1 nodes): `TelehealthApplicantRequestUniversalSafetyService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 424`** (1 nodes): `TelehealthApplicantSyntheticPromotionPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 425`** (2 nodes): `TelehealthRuntimeSafetyPolicy`, `TelehealthServiceRegistration`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 426`** (2 nodes): `ITelehealthProspectiveEligibilityGateway`, `SyntheticTelehealthProspectiveEligibilityGateway`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 427`** (2 nodes): `ITelehealthProspectiveIdentityProofingGateway`, `SyntheticTelehealthProspectiveIdentityProofingGateway`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 428`** (1 nodes): `TelehealthProspectiveSafetyTriagePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 429`** (1 nodes): `TelehealthProspectiveVisitPurposePolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 430`** (2 nodes): `ITelehealthVideoProvider`, `SyntheticTelehealthVideoProvider`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 432`** (1 nodes): `DevelopmentTestIdentityProviderEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 433`** (1 nodes): `FhirR4ValidationService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 434`** (1 nodes): `PatientEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 448`** (2 nodes): `Invoke-FixtureSql()`, `Set-FixturePortalState()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 450`** (1 nodes): `AuthorizationPolicyCatalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 451`** (1 nodes): `TelehealthApplicantInsuranceHandoffPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 452`** (1 nodes): `TelehealthApplicantPracticeReviewInboxService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 453`** (1 nodes): `TelehealthApplicantRequestComplaintTriageService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 454`** (1 nodes): `TelehealthApplicantRequestIntakePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 455`** (1 nodes): `TelehealthApplicantRequestParticipationEvaluationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 456`** (1 nodes): `TelehealthApplicantRequestQueueStatusRepository`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 457`** (1 nodes): `TelehealthAuthorizationTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 458`** (1 nodes): `TelehealthPatientQueueStatusProjectorTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 459`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsProtector`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 460`** (1 nodes): `TelehealthProspectiveMemberInsuranceDetailsService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 461`** (1 nodes): `TelehealthProspectiveSafetyTriageService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 462`** (1 nodes): `TelehealthProspectiveVisitPurposeService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 463`** (1 nodes): `StaffAccessContextServiceTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 467`** (1 nodes): `ExternalLaboratoryFhirIntakeEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 468`** (1 nodes): `FhirR4Endpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 469`** (1 nodes): `IntegrationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 486`** (1 nodes): `TelehealthApplicantRequestEligibilityPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 487`** (1 nodes): `TelehealthApplicantRequestInsuranceSourcePolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 488`** (1 nodes): `TelehealthApplicantRequestLocationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 489`** (1 nodes): `TelehealthApplicantRequestOperationalReviewSubmissionPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 490`** (1 nodes): `TelehealthApplicantRequestPracticeNetworkPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 491`** (1 nodes): `TelehealthApplicantRequestQueueAuthorizationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 492`** (1 nodes): `TelehealthAuthorizationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 493`** (1 nodes): `TelehealthPatientQueueStatusProjector`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 494`** (1 nodes): `AdministrationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 495`** (1 nodes): `AdministrativeReferenceEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 496`** (1 nodes): `AppointmentEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 497`** (1 nodes): `BillingEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 498`** (1 nodes): `ClinicalFormEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 499`** (1 nodes): `ClinicalListEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 500`** (1 nodes): `ClinicalWorkflowEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 501`** (1 nodes): `ConfigurationEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 502`** (1 nodes): `DocumentEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 503`** (1 nodes): `DocumentTemplateEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 504`** (1 nodes): `EncounterEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 505`** (1 nodes): `InventoryEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 506`** (1 nodes): `ManagedRecordEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 507`** (1 nodes): `MessageEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 508`** (1 nodes): `OfficeNoteEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 509`** (1 nodes): `PatientEngagementEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 510`** (1 nodes): `PatientPortalEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 511`** (1 nodes): `ProcedureEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 512`** (1 nodes): `ReportEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 513`** (1 nodes): `TherapyGroupEndpoints`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 514`** (2 nodes): `integration_inbox`, `integration_outbox`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 515`** (2 nodes): `coding_catalog_audit_events`, `coding_catalogs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 516`** (2 nodes): `form_option_lists`, `form_option_values`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 517`** (2 nodes): `clinical_alert_rules`, `encounter_clinical_alert_acknowledgments`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 518`** (2 nodes): `patient_record_requests`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 519`** (2 nodes): `patient_sdoh_assessments`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 520`** (2 nodes): `recall_activity`, `recalls`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 521`** (2 nodes): `patient_duplicate_review_dispositions`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 522`** (2 nodes): `document_template_binary_versions`, `document_templates`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 523`** (2 nodes): `patient_xml_exchange_audits`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 524`** (2 nodes): `inventory_count_reconciliations`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 525`** (2 nodes): `practice_setting_revisions`, `practice_settings`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 526`** (2 nodes): `coding_catalog_revisions`, `coding_catalogs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 527`** (2 nodes): `form_option_list_revisions`, `form_option_lists`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 528`** (2 nodes): `form_layout_revisions`, `form_layouts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 529`** (2 nodes): `clinical_alert_rule_revisions`, `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 530`** (2 nodes): `module_catalog`, `module_catalog_revisions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 531`** (2 nodes): `api_client_registry`, `api_client_registry_revisions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 532`** (2 nodes): `inventory_lot_metadata_audits`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 533`** (2 nodes): `inventory_lot_destructions`, `inventory_lots`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 534`** (2 nodes): `inventory_controlled_locations`, `inventory_controlled_report_runs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 535`** (2 nodes): `inventory_controlled_report_exports`, `inventory_controlled_report_runs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 536`** (2 nodes): `coding_catalog_change_request_events`, `coding_catalog_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 537`** (2 nodes): `form_layout_change_request_events`, `form_layout_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 538`** (2 nodes): `form_option_list_change_request_events`, `form_option_list_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 539`** (2 nodes): `clinical_alert_rule_change_request_events`, `clinical_alert_rule_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 540`** (2 nodes): `module_change_request_events`, `module_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 541`** (2 nodes): `api_client_change_request_events`, `api_client_change_requests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 542`** (2 nodes): `lab_specimens`, `procedure_specimen_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 543`** (2 nodes): `recall_lifecycle_events`, `recalls`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 544`** (2 nodes): `lab_reports`, `lab_specimens`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 545`** (2 nodes): `patient_registration_duplicate_reviews`, `patients`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 548`** (1 nodes): `TelehealthApplicantConnectionPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 549`** (1 nodes): `TelehealthApplicantConnectionPolicyTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 550`** (1 nodes): `TelehealthApplicantRequestCreationPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 551`** (1 nodes): `TelehealthApplicantRequestQueueStatusPolicy`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 552`** (1 nodes): `TelehealthApplicantRequestQueueStatusService`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 553`** (1 nodes): `TelehealthStateMachineTests`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 554`** (2 nodes): `pharmacies`, `prescriptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 555`** (1 nodes): `AzureOperationsOptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 556`** (1 nodes): `DatabaseConnectionOptions`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 557`** (1 nodes): `schema_migrations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 558`** (1 nodes): `statement_email_outbox`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 559`** (1 nodes): `phi_access_audit_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 560`** (1 nodes): `encounter_audit_events`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 561`** (1 nodes): `clinical_alert_rules`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 562`** (1 nodes): `module_catalog`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 563`** (1 nodes): `api_client_registry`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 564`** (1 nodes): `office_notes`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 565`** (1 nodes): `address_book_contacts`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 566`** (1 nodes): `track_anything_types`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 567`** (1 nodes): `patient_education_resources`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 568`** (1 nodes): `document_templates`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 569`** (1 nodes): `inventory_controlled_action_attestations`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 570`** (1 nodes): `AvenChart.Api.csproj`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AdministrationRepository` connect `Community 31` to `Community 34`, `Community 145`, `Community 162`, `Community 119`, `Community 101`, `Community 129`, `Community 189`, `Community 92`, `Community 108`, `Community 112`?**
  _High betweenness centrality (0.043) - this node is a cross-community bridge._
- **Why does `PatientPortalRepository` connect `Community 27` to `Community 41`, `Community 140`, `Community 141`, `Community 164`, `Community 80`, `Community 68`, `Community 292`, `Community 293`, `Community 147`, `Community 320`?**
  _High betweenness centrality (0.040) - this node is a cross-community bridge._
- **Why does `TelehealthEndpoints` connect `Community 11` to `Community 9`?**
  _High betweenness centrality (0.021) - this node is a cross-community bridge._
- **What connects `AccessibilityFinding`, `clinicianFixture`, `codingEncounter` to the rest of the system?**
  _994 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.0069079970957369565 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.020159151193633953 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.011159546643417612 - nodes in this community are weakly interconnected._