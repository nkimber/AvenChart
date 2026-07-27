export type PatientActivityCounts = {
  appointments: number
  encounters: number
  prescriptions: number
  billingItems: number
  labOrders: number
  messages: number
  problems: number
  allergies: number
  medications: number
}

export type PatientListItem = {
  canonicalId: string
  legacyPid: number
  pubpid: string
  displayName: string
  firstName: string
  lastName: string
  preferredName?: string | null
  sex?: string | null
  dateOfBirth: string
  age: number
  cohort?: string | null
  purpose?: string | null
  phone?: string | null
  phoneHome?: string | null
  phoneCell?: string | null
  email?: string | null
  providerId?: number | null
  facilityId?: number | null
  facilityName?: string | null
  primaryProviderName?: string | null
  counts: PatientActivityCounts
}

export type PatientTimelineItem = {
  id: string
  date: string
  time?: string | null
  title: string
  status?: string | null
  providerName?: string | null
  facilityName?: string | null
}

export type PatientInsuranceItem = {
  id: string
  type?: string | null
  provider?: string | null
  planName?: string | null
  policyNumber?: string | null
  groupNumber?: string | null
  relationship?: string | null
  subscriberFirstName?: string | null
  subscriberMiddleName?: string | null
  subscriberLastName?: string | null
  subscriberDateOfBirth?: string | null
  subscriberSex?: string | null
  subscriberStreet?: string | null
  subscriberStreetLine2?: string | null
  subscriberCity?: string | null
  subscriberState?: string | null
  subscriberPostalCode?: string | null
  subscriberCountry?: string | null
  subscriberPhone?: string | null
  subscriberEmployer?: string | null
  subscriberEmployerStreet?: string | null
  subscriberEmployerStreetLine2?: string | null
  subscriberEmployerCity?: string | null
  subscriberEmployerState?: string | null
  subscriberEmployerPostalCode?: string | null
  subscriberEmployerCountry?: string | null
}

export type PatientCareTeamMember = {
  id: number
  userId?: number | null
  contactId?: number | null
  memberType: string
  memberName?: string | null
  role: string
  roleDisplay: string
  facilityId?: number | null
  facilityName?: string | null
  providerSince?: string | null
  status: string
  statusDisplay: string
  note?: string | null
}

export type PatientCareTeamSummary = {
  teamName: string
  teamStatus: string
  teamStatusDisplay: string
  members: PatientCareTeamMember[]
}

export type PatientHistorySummary = {
  coffee?: string | null
  tobacco?: string | null
  alcohol?: string | null
  sleepPatterns?: string | null
  exercisePatterns?: string | null
  seatbeltUse?: string | null
  counseling?: string | null
  hazardousActivities?: string | null
  recreationalDrugs?: string | null
  lastPhysicalExam?: string | null
  lastMammogram?: string | null
  lastProstateExam?: string | null
  lastColonoscopy?: string | null
  lastEcg?: string | null
  lastRetinal?: string | null
  lastFluvax?: string | null
  lastPneuvax?: string | null
  lastLdl?: string | null
  lastHemoglobin?: string | null
  lastPsa?: string | null
  lastExamResults?: string | null
  historyMother?: string | null
  historyFather?: string | null
  historySiblings?: string | null
  historyOffspring?: string | null
  historySpouse?: string | null
  relativesCancer?: string | null
  relativesTuberculosis?: string | null
  relativesDiabetes?: string | null
  relativesHighBloodPressure?: string | null
  relativesHeartProblems?: string | null
  relativesStroke?: string | null
  relativesEpilepsy?: string | null
  relativesMentalIllness?: string | null
  relativesSuicide?: string | null
  appendectomyDate?: string | null
  tonsillectomyDate?: string | null
  cholecystectomyDate?: string | null
  heartSurgeryDate?: string | null
  hysterectomyDate?: string | null
  herniaRepairDate?: string | null
  hipReplacementDate?: string | null
  kneeReplacementDate?: string | null
  additionalHistory?: string | null
  exams?: string | null
  recordedAt?: string | null
}

export type PatientDuplicateCandidate = {
  canonicalId: string
  legacyPid: number
  pubpid: string
  displayName: string
  firstName: string
  lastName: string
  dateOfBirth: string
  phone?: string | null
  phoneHome?: string | null
  phoneCell?: string | null
  email?: string | null
  matchScore: number
  matchReasons: string[]
}

export type PatientDuplicateSearchResponse = {
  datasetId: string
  datasetVersion: string
  firstName?: string | null
  lastName?: string | null
  dateOfBirth?: string | null
  phone?: string | null
  email?: string | null
  limit: number
  totalCandidates: number
  candidates: PatientDuplicateCandidate[]
}

export type PatientMergePreviewPatient = {
  canonicalId: string
  legacyPid: number
  pubpid: string
  displayName: string
  firstName: string
  lastName: string
  dateOfBirth: string
  phoneHome?: string | null
  phoneCell?: string | null
  email?: string | null
}

export type PatientMergePreviewResponse = {
  datasetId: string
  datasetVersion: string
  previewOnly: boolean
  targetPatient: PatientMergePreviewPatient
  sourcePatient: PatientMergePreviewPatient
  targetCounts: PatientActivityCounts
  sourceCounts: PatientActivityCounts
  combinedCounts: PatientActivityCounts
  matchScore: number
  matchReasons: string[]
  safeguards: string[]
}

export type PatientInsuranceMutationInput = {
  type: string
  provider: string
  planName: string
  policyNumber: string
  groupNumber: string
  relationship: string
  subscriberFirstName: string
  subscriberMiddleName: string
  subscriberLastName: string
  subscriberDateOfBirth: string
  subscriberSex: string
  subscriberStreet: string
  subscriberStreetLine2: string
  subscriberCity: string
  subscriberState: string
  subscriberPostalCode: string
  subscriberCountry: string
  subscriberPhone: string
  subscriberEmployer: string
  subscriberEmployerStreet: string
  subscriberEmployerStreetLine2: string
  subscriberEmployerCity: string
  subscriberEmployerState: string
  subscriberEmployerPostalCode: string
  subscriberEmployerCountry: string
}

export type PatientChartSummary = PatientListItem & {
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  hipaaAllowSms?: string | null
  hipaaAllowEmail?: string | null
  maritalStatus?: string | null
  occupation?: string | null
  race?: string | null
  ethnicity?: string | null
  interpreter?: string | null
  familySize?: string | null
  monthlyIncome?: string | null
  homeless?: string | null
  financialReviewDate?: string | null
  motherName?: string | null
  guardianName?: string | null
  guardianRelationship?: string | null
  guardianPhone?: string | null
  guardianEmail?: string | null
  guardianSex?: string | null
  guardianAddress?: string | null
  guardianCity?: string | null
  guardianState?: string | null
  guardianPostalCode?: string | null
  guardianCountry?: string | null
  guardianWorkPhone?: string | null
  employerName?: string | null
  employerStreet?: string | null
  employerCity?: string | null
  employerState?: string | null
  employerPostalCode?: string | null
  employerCountry?: string | null
  portalEnabled: boolean
  portalAccount?: PatientPortalAccountSummary | null
  registrationDate: string
  deceasedDate?: string | null
  deceasedReason?: string | null
  careTeam?: PatientCareTeamSummary | null
  insurance: PatientInsuranceItem[]
  history?: PatientHistorySummary | null
  duplicateCandidates: PatientDuplicateCandidate[]
  nextAppointment?: PatientTimelineItem | null
  latestEncounter?: PatientTimelineItem | null
}

export type PatientPortalAccountSummary = {
  portalEnabled: boolean
  accessStatusLabel: string
  cmsPortalLogin?: string | null
  hasAccount: boolean
  portalUsername?: string | null
  portalLoginUsername?: string | null
  passwordStatus?: number | null
  passwordStatusLabel: string
  oneTimeLinkPending: boolean
  resetStatusLabel: string
}

export type PatientPortalAccountResetUpdate = {
  oneTimeLinkPending: boolean
}

export type PatientPortalAccountAccessUpdate = {
  portalEnabled: boolean
}

export type PatientSearchResponse = {
  datasetId: string
  datasetVersion: string
  search?: string | null
  limit: number
  totalMatches: number
  patients: PatientListItem[]
}

export type PatientContactUpdate = {
  phoneHome: string
  phoneCell: string
  email: string
  hipaaAllowSms: string
  hipaaAllowEmail: string
}

export type PatientDemographicsUpdate = {
  firstName: string
  lastName: string
  preferredName: string
  sex: string
  dateOfBirth: string
  street: string
  city: string
  state: string
  postalCode: string
  maritalStatus: string
  occupation: string
  race: string
  ethnicity: string
  interpreter: string
  familySize: string
  monthlyIncome: string
  homeless: string
  financialReviewDate: string
}

export type PatientDeceasedStatusUpdate = {
  deceasedDate: string
  deceasedReason: string
}

export type PatientGuardianContactUpdate = {
  motherName: string
  guardianName: string
  guardianRelationship: string
  guardianPhone: string
  guardianEmail: string
  guardianSex: string
  guardianAddress: string
  guardianCity: string
  guardianState: string
  guardianPostalCode: string
  guardianCountry: string
  guardianWorkPhone: string
}

export type PatientEmployerUpdate = {
  employerName: string
  employerStreet: string
  employerCity: string
  employerState: string
  employerPostalCode: string
  employerCountry: string
}

export type PatientProviderAssignmentOption = {
  id: number
  displayName: string
  facilityId?: number | null
  facilityName?: string | null
}

export type PatientProviderAssignmentOptionsResponse = {
  datasetId: string
  datasetVersion: string
  providers: PatientProviderAssignmentOption[]
}

export type PatientCareTeamContactOption = {
  id: number
  displayName: string
  relationship?: string | null
  phone?: string | null
  email?: string | null
}

export type PatientCareTeamOptionsResponse = {
  datasetId: string
  datasetVersion: string
  providers: PatientProviderAssignmentOption[]
  contacts: PatientCareTeamContactOption[]
}

export type PatientProviderAssignmentUpdate = {
  providerId: number | null
}

export type PatientCareTeamMemberUpdate = {
  userId: number | null
  contactId: number | null
  role: string
  facilityId: number | null
  providerSince: string
  status: string
  note: string
}

export type PatientCareTeamUpdate = {
  teamName: string
  teamStatus: string
  members: PatientCareTeamMemberUpdate[]
  userId?: number | null
  role?: string
  facilityId?: number | null
  providerSince?: string
  status?: string
  note?: string
}

export type PatientRegistrationInput = PatientDemographicsUpdate & {
  pubpid: string
  phoneHome: string
  phoneCell: string
  email: string
  hipaaAllowSms: string
  hipaaAllowEmail: string
}

export class PatientRegistrationValidationError extends Error {
  readonly messages: string[]

  constructor(messages: string[]) {
    super(`Patient registration validation failed: ${messages.join('; ')}`)
    this.name = 'PatientRegistrationValidationError'
    this.messages = messages
  }
}

export type AppointmentListItem = {
  id: string
  seriesRootId: string
  isRecurringSeries: boolean
  isVirtualOccurrence: boolean
  occurrenceNumber?: number | null
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  date: string
  startTime: string
  durationMinutes: number
  title: string
  status?: string | null
  room?: string | null
  categoryId?: number | null
  categoryName?: string | null
  providerId?: number | null
  providerName?: string | null
  facilityId?: number | null
  facilityName?: string | null
  billingLocationId?: number | null
  billingLocationName?: string | null
  comments?: string | null
  recurrenceType: number
  repeatFrequency?: number | null
  repeatUnit?: number | null
  repeatOnNum?: number | null
  repeatOnDay?: number | null
  repeatOnFrequency?: number | null
  recurrenceDays: number[]
  recurrenceEndDate?: string | null
  recurrenceExdates: string[]
  recurrenceExceptionCount: number
  recurrenceLabel: string
  providerOverlapCount: number
  providerOverlapAppointmentIds: string[]
  patientOverlapCount: number
  patientOverlapAppointmentIds: string[]
  roomOverlapCount: number
  roomOverlapAppointmentIds: string[]
  reminderDue: boolean
  reminderStatus: string
  reminderChannel: string
  reminderContact?: string | null
  reminderLeadDays?: number | null
  convertedEncounterId?: number | null
  convertedEncounterDate?: string | null
  convertedBillingLineCount: number
}

export type AppointmentDetail = AppointmentListItem & {
  firstName: string
  lastName: string
  sex?: string | null
  dateOfBirth: string
  patientPurpose?: string | null
}

export type AppointmentCreateInput = {
  patientId: string
  providerId?: number | null
  title: string
  date: string
  startTime: string
  durationMinutes: number
  facilityId?: number | null
  billingLocationId?: number | null
  categoryId?: number | null
  room?: string | null
  comments?: string | null
  recurrenceType?: number | null
  repeatFrequency?: number | null
  repeatUnit?: number | null
  repeatOnNum?: number | null
  repeatOnDay?: number | null
  repeatOnFrequency?: number | null
  recurrenceDays?: number[] | null
  recurrenceEndDate?: string | null
  recurrenceExdates?: string[] | null
  enforceConflictPolicy?: boolean
}

export type AppointmentAvailabilityValidationInput = {
  patientId: string
  providerId?: number | null
  date: string
  startTime: string
  durationMinutes: number
  facilityId?: number | null
  room?: string | null
  excludeAppointmentId?: string | null
}

export type AppointmentAvailabilityConflict = {
  appointmentId: string
  conflictType: string
  patientId: string
  patientDisplayName: string
  date: string
  startTime: string
  endTime: string
  title: string
}

export type AppointmentAvailabilityValidationResponse = {
  available: boolean
  validationStatus: string
  date: string
  startTime: string
  endTime: string
  durationMinutes: number
  patientKnown: boolean
  providerId?: number | null
  providerName?: string | null
  providerAvailable: boolean
  facilityId?: number | null
  facilityName?: string | null
  facilityAvailable: boolean
  withinBusinessHours: boolean
  conflictCount: number
  conflicts: AppointmentAvailabilityConflict[]
  messages: string[]
}

export type AppointmentWaitlistItem = {
  appointmentId: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  date: string
  startTime: string
  endTime: string
  durationMinutes: number
  title: string
  status?: string | null
  categoryId?: number | null
  categoryName?: string | null
  providerId?: number | null
  providerName?: string | null
  facilityId?: number | null
  facilityName?: string | null
  room?: string | null
  reason?: string | null
  daysUntilRequestedSlot: number
  priority: string
  reminderCreated: boolean
  reminderId?: string | null
  reminderStatus?: string | null
  reminderAssignedTo?: string | null
  reminderBody?: string | null
}

export type AppointmentWaitlistResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  totalWaiting: number
  items: AppointmentWaitlistItem[]
}

export type AppointmentReminderDispatchResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  appointmentId: string
  dispatchId: string
  auditId: string
  dispatchedAt: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  appointmentDate: string
  startTime: string
  endTime: string
  title: string
  reminderStatus: string
  reminderChannel: string
  reminderContact?: string | null
  reminderLeadDays?: number | null
  queueName: string
  dispatchStatus: string
  externalReference: string
  templateName: string
  messagePreview: string
  retryOfDispatchId?: string | null
  retryAttempt: number
}

export type AppointmentReminderTemplateOption = {
  templateId: string
  name: string
  channel: string
  queueName: string
  description: string
  isDefault: boolean
}

export type AppointmentReminderTemplateCatalogResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  templates: AppointmentReminderTemplateOption[]
}

export type AppointmentReminderDispatchInput = {
  templateId?: string | null
}

export type AppointmentReminderDispatchHistoryResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  eventCount: number
  entries: AppointmentReminderDispatchResponse[]
}

export type AppointmentUpdateInput = {
  providerId?: number | null
  title: string
  date: string
  startTime: string
  durationMinutes: number
  facilityId?: number | null
  billingLocationId?: number | null
  categoryId?: number | null
  room?: string | null
  status?: string | null
  comments?: string | null
  recurrenceType?: number | null
  repeatFrequency?: number | null
  repeatUnit?: number | null
  repeatOnNum?: number | null
  repeatOnDay?: number | null
  repeatOnFrequency?: number | null
  recurrenceDays?: number[] | null
  recurrenceEndDate?: string | null
  recurrenceExdates?: string[] | null
}

export type AppointmentOccurrenceRescheduleInput = {
  providerId?: number | null
  title: string
  date: string
  startTime: string
  durationMinutes: number
  facilityId?: number | null
  billingLocationId?: number | null
  categoryId?: number | null
  room?: string | null
  status?: string | null
  comments?: string | null
}

export type AppointmentStatusUpdate = {
  status: string
  title?: string | null
}

export type AppointmentSearchResponse = {
  datasetId: string
  datasetVersion: string
  patientId?: string | null
  fromDate?: string | null
  limit: number
  totalMatches: number
  appointments: AppointmentListItem[]
}

export type FlowBoardItem = {
  appointmentId: string
  patientId: string
  patientDisplayName: string
  startTime: string
  title: string
  room?: string | null
  providerName?: string | null
  facilityName?: string | null
  appointmentStatus?: string | null
  flowStatus: string
}

export type FlowBoardLane = {
  key: string
  label: string
  items: FlowBoardItem[]
}

export type FlowBoardResponse = {
  datasetId: string
  datasetVersion: string
  date: string
  lanes: FlowBoardLane[]
}

export type EncounterListItem = {
  id: number
  encounter: number
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  date: string
  reason?: string | null
  diagnosisCode?: string | null
  diagnosisText?: string | null
  categoryId?: number | null
  providerName?: string | null
  facilityName?: string | null
  sensitivity?: string | null
  referralSource?: string | null
  externalId?: string | null
  posCode?: number | null
  hasVitals: boolean
  hasSoapNote: boolean
  billingLineCount: number
}

export type EncounterVitals = {
  systolic?: number | null
  diastolic?: number | null
  bloodPressure?: string | null
  weight?: number | null
  height?: number | null
  temperature?: number | null
  pulse?: number | null
  respiration?: number | null
  bmi?: number | null
  oxygenSaturation?: number | null
}

export type EncounterSoapNote = {
  subjective?: string | null
  objective?: string | null
  assessment?: string | null
  plan?: string | null
}

export type EncounterSoapNoteTemplateOption = {
  templateId: string
  name: string
  category: string
  description: string
  subjective: string
  objective: string
  assessment: string
  plan: string
  isDefault: boolean
}

export type EncounterSoapNoteTemplateCatalogResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  templates: EncounterSoapNoteTemplateOption[]
}

export type EncounterDocumentAttachment = {
  id: number
  documentKey: string
  categoryId: number
  categoryName: string
  name: string
  docDate: string
  uploadedAt: string
  revisionAt: string
  currentVersion: number
  versionLabel: string
  versionStatus: string
  versionHistoryCount: number
  hasPriorVersions: boolean
  revisionHash?: string | null
  mimetype?: string | null
  sizeBytes?: number | null
  pages?: number | null
  storageMethod?: string | null
  fileName?: string | null
  url?: string | null
  hash?: string | null
  notes?: string | null
  deleted: number
  reviewStatus: string
  reviewedBy?: string | null
  reviewedAt?: string | null
  contentPreview?: string | null
  previewKind: string
  previewStatus: string
  thumbnailLabel: string
  thumbnailText: string
  canPreviewInline: boolean
  canDownload: boolean
  isScannedAttachment: boolean
  scanStatus: string
  captureSource: string
  scanPageCount: number
  ocrStatus: string
  lifecycleEvents: EncounterDocumentLifecycleEvent[]
}

export type EncounterDocumentLifecycleEvent = {
  code: string
  label: string
  occurredAt?: string | null
  actor?: string | null
  detail: string
}

export type EncounterDiagnosisCode = {
  code: string
  description?: string | null
  sources: string[]
  billingLineCount: number
  procedureOrderCount: number
  supportingBillingCodes: string[]
}

export type EncounterSignatureItem = {
  id: number
  tableName: string
  signerUserId: number | null
  signerUsername: string
  signedAt: string
  isLock: boolean
  amendment?: string | null
  hash: string
  signatureHash: string
}

export type EncounterAmendmentHistoryItem = {
  signatureId: number
  signerUsername: string
  signedAt: string
  isLock: boolean
  amendment: string
  hash: string
  signatureHash: string
}

export type EncounterDetail = EncounterListItem & {
  firstName: string
  lastName: string
  sex?: string | null
  dateOfBirth: string
  dateTime: string
  billingNote?: string | null
  sourceAppointmentId?: string | null
  vitals?: EncounterVitals | null
  soapNote?: EncounterSoapNote | null
  diagnosisCodes: EncounterDiagnosisCode[]
  billingLines: BillingLineItem[]
  claims: BillingClaimItem[]
  procedureOrders: ProcedureOrderItem[]
  signatures: EncounterSignatureItem[]
  amendmentHistory: EncounterAmendmentHistoryItem[]
  documents: EncounterDocumentAttachment[]
}

export type EncounterCreateInput = {
  patientId: string
  providerId?: number | null
  dateTime: string
  reason: string
  facilityId?: number | null
  billingFacilityId?: number | null
  sensitivity?: string | null
  referralSource?: string | null
  externalId?: string | null
  posCode?: number | null
  billingNote?: string | null
  sourceAppointmentId?: string | null
}

export type EncounterUpdateInput = {
  reason: string
  sensitivity?: string | null
  referralSource?: string | null
  externalId?: string | null
  posCode?: number | null
  billingNote?: string | null
}

export type EncounterVitalsCreateInput = {
  dateTime: string
  systolic?: number | null
  diastolic?: number | null
  weight?: number | null
  height?: number | null
  temperature?: number | null
  pulse?: number | null
  respiration?: number | null
  oxygenSaturation?: number | null
  note?: string | null
}

export type EncounterSoapNoteCreateInput = {
  dateTime: string
  subjective?: string | null
  objective?: string | null
  assessment?: string | null
  plan?: string | null
}

export type EncounterFormMutationResponse = {
  id: number
  detail: EncounterDetail
}

export type EncounterSignInput = {
  signerUsername: string
  signedAt: string
  isLock: boolean
  amendment?: string | null
}

export type EncounterSignatureMutationResponse = {
  id: number
  detail: EncounterDetail
}

export type EncounterDocumentCreateInput = {
  categoryId: number
  name: string
  docDate: string
  content: string
  notes?: string | null
}

export type EncounterBinaryDocumentCreateInput = {
  categoryId: number
  name: string
  docDate: string
  fileName: string
  mimetype: string
  contentBase64: string
  notes?: string | null
}

export type EncounterExternalLinkDocumentCreateInput = {
  categoryId: number
  name: string
  docDate: string
  url: string
  notes?: string | null
}

export type EncounterDocumentMutationResponse = {
  id: number
  detail: EncounterDetail
}

export type EncounterDocumentMoveInput = {
  targetEncounter: number
}

export type EncounterDocumentMoveResponse = {
  id: number
  sourceDetail: EncounterDetail
  targetDetail: EncounterDetail
}

export type EncounterSearchResponse = {
  datasetId: string
  datasetVersion: string
  patientId?: string | null
  fromDate: string
  limit: number
  totalMatches: number
  encounters: EncounterListItem[]
}

export type ProblemListItem = {
  id: string
  title: string
  diagnosis?: string | null
  date?: string | null
  comments?: string | null
  activity: number
}

export type AllergyListItem = {
  id: string
  title: string
  reaction?: string | null
  severity?: string | null
  date?: string | null
  comments?: string | null
  activity: number
  listOptionId?: string | null
}

export type MedicationListItem = {
  id: string
  title: string
  diagnosis?: string | null
  date?: string | null
  comments?: string | null
  activity: number
}

export type MedicationDuplicateSummary = {
  normalizedTitle: string
  displayTitle: string
  activeCount: number
  medicationIds: string[]
  firstDate?: string | null
  latestDate?: string | null
  diagnoses: string[]
}

export type MedicationReconciliationSummary = {
  normalizedTitle: string
  displayTitle: string
  status: string
  medicationCount: number
  prescriptionCount: number
  medicationIds: string[]
  prescriptionIds: string[]
  medicationTitles: string[]
  prescriptionDrugs: string[]
  diagnoses: string[]
}

export type MedicationVocabularyItem = {
  rxNormCode: string
  drugName: string
  displayName: string
  form: string
  strength: string
  route: string
  doseAmount?: number | null
  doseUnit?: string | null
  frequency?: string | null
  durationDays?: number | null
  controlledSubstanceSchedule?: string | null
}

export type PrescriptionListItem = {
  id: string
  drug: string
  dosage?: string | null
  quantity?: string | null
  doseAmount?: number | null
  doseUnit?: string | null
  frequency?: string | null
  durationDays?: number | null
  route?: string | null
  rxNormCode?: string | null
  controlledSubstanceSchedule?: string | null
  controlledSubstanceReviewRequired: boolean
  controlledSubstanceReason?: string | null
  diagnosis?: string | null
  startDate?: string | null
  endDate?: string | null
  refills: number
  active: number
  note?: string | null
  encounter?: number | null
  providerName?: string | null
  pharmacyId?: number | null
  pharmacyName?: string | null
  pharmacyNcpdp?: number | null
  erxUploaded: number
  erxSentAt?: string | null
  erxPayload?: string | null
}

export type PrescriptionDiagnosisInteractionSummary = {
  diagnosis: string
  status: string
  problemId?: string | null
  problemTitle?: string | null
  prescriptionCount: number
  prescriptionIds: string[]
  drugs: string[]
}

export type PrescriptionRefillRequestItem = {
  messageId: number
  title: string
  requestDate: string
  patientDisplayName: string
  portalUsername: string
  prescriptionId: string
  drug: string
  dosage?: string | null
  quantity?: string | null
  route?: string | null
  currentRefills: number
  status: string
  patientNote?: string | null
  body: string
}

export type ImmunizationListItem = {
  id: number
  key: string
  immunizationId?: number | null
  cvxCode?: string | null
  vaccine: string
  administeredAt?: string | null
  manufacturer?: string | null
  lotNumber?: string | null
  administeredBy?: string | null
  educationDate?: string | null
  visDate?: string | null
  amountAdministered?: number | null
  amountAdministeredUnit?: string | null
  expirationDate?: string | null
  route?: string | null
  administrationSite?: string | null
  completionStatus?: string | null
  informationSource?: string | null
  note?: string | null
  encounter?: number | null
}

export type ClinicalListsResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  firstName: string
  lastName: string
  problems: ProblemListItem[]
  allergies: AllergyListItem[]
  medications: MedicationListItem[]
  medicationDuplicates: MedicationDuplicateSummary[]
  medicationReconciliations: MedicationReconciliationSummary[]
  immunizations: ImmunizationListItem[]
  prescriptions: PrescriptionListItem[]
  prescriptionDiagnosisInteractions: PrescriptionDiagnosisInteractionSummary[]
  prescriptionRefillRequests: PrescriptionRefillRequestItem[]
}

export type ClinicalAllergyCreateInput = {
  patientId: string
  title: string
  dateTime: string
  comments: string
  reaction: string
  severity: string
  listOptionId?: string | null
}

export type ClinicalProblemCreateInput = {
  patientId: string
  title: string
  dateTime: string
  diagnosis?: string | null
  comments: string
}

export type ClinicalMedicationCreateInput = {
  patientId: string
  title: string
  dateTime: string
  diagnosis?: string | null
  comments: string
}

export type ClinicalListDeactivateInput = {
  comments: string
}

export type ClinicalListMutationResponse = {
  id: string
  detail: ClinicalListsResponse
}

export type ClinicalPrescriptionPharmacyRouteResponse = {
  id: string
  routed: boolean
  failureReason?: string | null
  detail: ClinicalListsResponse
}

export type ClinicalPrescriptionAuditEventItem = {
  eventId: string
  prescriptionId: string
  action: string
  occurredAt: string
  actor: string
  detail?: string | null
  beforeRefills?: number | null
  afterRefills?: number | null
  pharmacyId?: number | null
  pharmacyName?: string | null
  failureReason?: string | null
}

export type ClinicalPrescriptionAuditHistoryResponse = {
  prescriptionId: string
  eventCount: number
  events: ClinicalPrescriptionAuditEventItem[]
}

export type ClinicalPrescriptionCreateInput = {
  patientId: string
  providerId?: number | null
  startDate: string
  drug: string
  rxNormCode?: string | null
  dosage: string
  quantity: string
  doseAmount?: number | null
  doseUnit?: string | null
  frequency?: string | null
  durationDays?: number | null
  route?: string | null
  refills: number
  note: string
  diagnosis: string
}

export type ClinicalPrescriptionDeactivateInput = {
  endDate: string
  note: string
}

export type ClinicalPrescriptionRefillInput = {
  refillDate: string
  additionalRefills: number
  note: string
}

export type ClinicalPrescriptionPharmacyRouteInput = {
  pharmacyId: number
  sentAt: string
  note: string
}

export type ClinicalPrescriptionRefillApprovalInput = {
  refillDate: string
  additionalRefills: number
  note: string
}

export type ClinicalImmunizationCreateInput = {
  patientId: string
  encounter?: number | null
  immunizationId?: number | null
  cvxCode?: string | null
  vaccine: string
  administeredAt: string
  manufacturer?: string | null
  lotNumber?: string | null
  administeredById?: number | null
  administeredBy?: string | null
  educationDate?: string | null
  visDate?: string | null
  amountAdministered?: number | null
  amountAdministeredUnit?: string | null
  expirationDate?: string | null
  route?: string | null
  administrationSite?: string | null
  completionStatus?: string | null
  informationSource?: string | null
  note?: string | null
}

export type ClinicalImmunizationErrorInput = {
  note: string
}

export type PatientMessageItem = {
  id: string
  date?: string | null
  title?: string | null
  body?: string | null
  status?: string | null
  assignedTo?: string | null
  portalRelation?: string | null
  isEncrypted: boolean
  updatedBy?: number | null
  updatedAt?: string | null
  deleted: number
}

export type PatientMessagesResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  firstName: string
  lastName: string
  portalEnabled: boolean
  messages: PatientMessageItem[]
}

export type PatientDocumentItem = {
  id: number
  documentKey: string
  patientId: string
  legacyPid: number
  categoryId: number
  categoryName: string
  name: string
  docDate: string
  uploadedAt: string
  revisionAt: string
  currentVersion: number
  versionLabel: string
  versionStatus: string
  versionHistoryCount: number
  hasPriorVersions: boolean
  revisionHash?: string | null
  mimetype?: string | null
  sizeBytes?: number | null
  pages?: number | null
  encounter?: number | null
  storageMethod?: string | null
  fileName?: string | null
  url?: string | null
  hash?: string | null
  documentationOf?: string | null
  notes?: string | null
  deleted: number
  reviewStatus: string
  reviewedBy?: string | null
  reviewedAt?: string | null
  contentPreview?: string | null
  previewKind: string
  previewStatus: string
  thumbnailLabel: string
  thumbnailText: string
  thumbnailDataUri?: string | null
  canPreviewInline: boolean
  canDownload: boolean
  isScannedAttachment: boolean
  scanStatus: string
  captureSource: string
  scanPageCount: number
  ocrStatus: string
  lifecycleEvents: PatientDocumentLifecycleEvent[]
}

export type PatientDocumentLifecycleEvent = {
  code: string
  label: string
  occurredAt?: string | null
  actor?: string | null
  detail: string
}

export type PatientDocumentVersionItem = {
  version: number
  versionLabel: string
  versionStatus: string
  capturedAt: string
  fileName?: string | null
  mimetype?: string | null
  sizeBytes?: number | null
  pages?: number | null
  hash?: string | null
  contentPreview: string
}

export type PatientDocumentOcrQueueItem = {
  id: number
  documentKey: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  categoryId: number
  categoryName: string
  name: string
  docDate: string
  uploadedAt: string
  mimetype?: string | null
  fileName?: string | null
  pages?: number | null
  encounter?: number | null
  captureSource: string
  scanPageCount: number
  ocrStatus: string
  queueStatus: string
  priority: string
  notes?: string | null
}

export type PatientDocumentOcrQueueResponse = {
  datasetId: string
  datasetVersion: string
  count: number
  items: PatientDocumentOcrQueueItem[]
}

export type PatientDocumentRoutingQueueItem = {
  id: number
  documentKey: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  categoryId: number
  categoryName: string
  name: string
  docDate: string
  uploadedAt: string
  mimetype?: string | null
  fileName?: string | null
  encounter?: number | null
  reviewStatus: string
  queueStatus: string
  routeDestination: string
  priority: string
  routingReason: string
  notes?: string | null
}

export type PatientDocumentRoutingQueueResponse = {
  datasetId: string
  datasetVersion: string
  count: number
  items: PatientDocumentRoutingQueueItem[]
}

export type PatientDocumentRetentionPolicyItem = {
  id: number
  documentKey: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  categoryId: number
  categoryName: string
  name: string
  docDate: string
  uploadedAt: string
  mimetype?: string | null
  fileName?: string | null
  encounter?: number | null
  retentionClass: string
  retentionYears: number
  retainUntil: string
  dispositionStatus: string
  policyBasis: string
  notes?: string | null
}

export type PatientDocumentRetentionPolicyResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  count: number
  eligibleCount: number
  items: PatientDocumentRetentionPolicyItem[]
}

export type PatientDocumentRetentionDispositionInput = {
  disposedBy: string
  reason: string
}

export type PatientDocumentRetentionDispositionResponse = {
  id: number
  dispositionStatus: string
  disposedBy: string
  disposedAt: string
  retainUntil: string
  detail: PatientDocumentsResponse
  policy: PatientDocumentRetentionPolicyResponse
}

export type PatientDocumentOcrCompleteInput = {
  extractedText: string
  completedBy: string
}

export type PatientDocumentOcrCompleteResponse = {
  id: number
  ocrStatus: string
  completedBy: string
  completedAt: string
  document: PatientDocumentContentResponse
  queue: PatientDocumentOcrQueueResponse
}

export type PatientDocumentsResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  firstName: string
  lastName: string
  count: number
  documents: PatientDocumentItem[]
}

export type PatientDocumentCreateInput = {
  patientId: string
  categoryId: number
  name: string
  docDate: string
  encounter?: number | null
  content: string
  notes?: string | null
}

export type PatientDocumentBinaryCreateInput = {
  patientId: string
  categoryId: number
  name: string
  docDate: string
  encounter?: number | null
  fileName: string
  mimetype: string
  contentBase64: string
  notes?: string | null
}

export type PatientDocumentScannerCaptureInput = {
  patientId: string
  categoryId: number
  name: string
  docDate: string
  encounter?: number | null
  captureSource: string
  pageCount: number
  capturedBy: string
  notes?: string | null
}

export type PatientDocumentExternalLinkCreateInput = {
  patientId: string
  categoryId: number
  name: string
  docDate: string
  encounter?: number | null
  url: string
  notes?: string | null
}

export type PatientDocumentMetadataUpdateInput = {
  categoryId: number
  name: string
  docDate: string
  encounter?: number | null
  notes?: string | null
}

export type PatientDocumentContentReplaceInput = {
  fileName: string
  content: string
}

export type PatientDocumentBinaryContentReplaceInput = {
  fileName: string
  mimetype: string
  contentBase64: string
}

export type PatientDocumentContentResponse = {
  id: number
  documentKey: string
  patientId: string
  legacyPid: number
  categoryId: number
  categoryName: string
  name: string
  fileName: string
  docDate: string
  uploadedAt: string
  revisionAt: string
  currentVersion: number
  versionLabel: string
  versionStatus: string
  versionHistoryCount: number
  hasPriorVersions: boolean
  revisionHash?: string | null
  mimetype?: string | null
  sizeBytes?: number | null
  pages?: number | null
  encounter?: number | null
  storageMethod?: string | null
  url?: string | null
  hash?: string | null
  documentationOf?: string | null
  notes?: string | null
  reviewStatus: string
  reviewedBy?: string | null
  reviewedAt?: string | null
  content: string
  contentBase64?: string | null
  isBinary: boolean
  previewKind: string
  previewStatus: string
  thumbnailLabel: string
  thumbnailText: string
  canPreviewInline: boolean
  canDownload: boolean
  isScannedAttachment: boolean
  scanStatus: string
  captureSource: string
  scanPageCount: number
  ocrStatus: string
  lifecycleEvents: PatientDocumentLifecycleEvent[]
  versionHistory: PatientDocumentVersionItem[]
}

export type PatientDocumentSignInput = {
  reviewStatus: string
  reviewedBy: string
}

export type PatientDocumentMutationResponse = {
  id: number
  detail: PatientDocumentsResponse
}

export type PatientMessageCreateInput = {
  patientId: string
  title: string
  body: string
  assignedTo: string
}

export type PatientMessageStatusUpdateInput = {
  status: string
  body: string
}

export type PatientMessageContentUpdateInput = {
  title: string
  body: string
}

export type PatientMessageAssignmentUpdateInput = {
  assignedTo: string
}

export type PatientMessageReplyInput = {
  body: string
  assignedTo: string
}

export type PatientMessageMutationResponse = {
  id: string
  detail: PatientMessagesResponse
}

export type ProcedureResultItem = {
  id: number
  code?: string | null
  text?: string | null
  units?: string | null
  result?: string | null
  range?: string | null
  abnormal?: string | null
  resultDate: string
  resultStatus?: string | null
  currentVersion: number
  versionLabel: string
  versionHistoryCount: number
  hasPriorVersions: boolean
  versionHistory: ProcedureResultVersionItem[]
}

export type ProcedureResultVersionItem = {
  version: number
  versionLabel: string
  versionStatus: string
  capturedAt: string
  code?: string | null
  text?: string | null
  units?: string | null
  result?: string | null
  range?: string | null
  abnormal?: string | null
  resultDate: string
  resultStatus?: string | null
}

export type ProcedureReportItem = {
  id: number
  dateCollected: string
  reportDate: string
  specimenNumber?: string | null
  status?: string | null
  reviewStatus?: string | null
  reviewedBy?: string | null
  reviewedAt?: string | null
  notes?: string | null
  results: ProcedureResultItem[]
}

export type ProcedureReportReviewQueueItem = {
  reportId: number
  orderId: number
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  orderDate: string
  providerId?: number | null
  providerName?: string | null
  labId?: number | null
  labName?: string | null
  procedureCode?: string | null
  procedureName?: string | null
  reportDate: string
  reportStatus?: string | null
  reviewStatus?: string | null
  reviewedBy?: string | null
  reviewedAt?: string | null
  specimenNumber?: string | null
  notes?: string | null
}

export type ProcedureReportReviewQueueResponse = {
  datasetId: string
  datasetVersion: string
  statusFilter: string
  patientFilter?: string | null
  providerFilter?: number | null
  labFilter?: number | null
  fromDate?: string | null
  toDate?: string | null
  limit: number
  totalReports: number
  reviewedReports: number
  unreviewedReports: number
  reports: ProcedureReportReviewQueueItem[]
}

export type ProcedureOrderQueueItem = {
  orderId: number
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  encounterId?: number | null
  orderDate: string
  providerId?: number | null
  providerName?: string | null
  labId?: number | null
  labName?: string | null
  procedureCode?: string | null
  procedureName?: string | null
  procedureType?: string | null
  orderPriority?: string | null
  orderStatus?: string | null
  dateTransmitted?: string | null
  reportCount: number
  resultCount: number
  specimenCount: number
  canTransmit: boolean
  queueState: string
  instructions?: string | null
}

export type ProcedureOrderQueueResponse = {
  datasetId: string
  datasetVersion: string
  statusFilter: string
  patientFilter?: string | null
  providerFilter?: number | null
  labFilter?: number | null
  fromDate?: string | null
  toDate?: string | null
  limit: number
  totalOrders: number
  readyToSendOrders: number
  transmittedPendingOrders: number
  reportedOrders: number
  scheduledOrders: number
  completedOrders: number
  orders: ProcedureOrderQueueItem[]
}

export type ProcedureLabProviderItem = {
  id: number
  name: string
  labDirectorId?: number | null
  labDirectorName?: string | null
  labDirectorType?: string | null
  npi?: string | null
  protocol?: string | null
  usage?: string | null
  direction?: string | null
  sendApplicationId?: string | null
  sendFacilityId?: string | null
  receiveApplicationId?: string | null
  receiveFacilityId?: string | null
  remoteHost?: string | null
  login?: string | null
  password?: string | null
  ordersPath?: string | null
  resultsPath?: string | null
  notes?: string | null
  active: boolean
  orderCount: number
  reportCount: number
  futureOrderCount: number
}

export type ProcedureLabProviderDirectoryResponse = {
  datasetId: string
  datasetVersion: string
  includeInactive: boolean
  totalProviders: number
  activeProviders: number
  inactiveProviders: number
  providers: ProcedureLabProviderItem[]
}

export type ProcedureLabProviderMutationInput = {
  name: string
  labDirectorId?: number | null
  npi?: string | null
  protocol?: string | null
  usage?: string | null
  direction?: string | null
  sendApplicationId?: string | null
  sendFacilityId?: string | null
  receiveApplicationId?: string | null
  receiveFacilityId?: string | null
  remoteHost?: string | null
  login?: string | null
  password?: string | null
  ordersPath?: string | null
  resultsPath?: string | null
  notes?: string | null
  active: boolean
}

export type ProcedureLabProviderMutationResponse = {
  id: number
  directory: ProcedureLabProviderDirectoryResponse
}

export type ProcedureLabProviderAddressBookItem = {
  id: number
  organization: string
  type: string
  active: boolean
}

export type ProcedureLabProviderAddressBookResponse = {
  datasetId: string
  datasetVersion: string
  organizations: ProcedureLabProviderAddressBookItem[]
}

export type ProcedureLabProviderAddressBookMutationInput = {
  organization: string
  type?: string | null
  active: boolean
}

export type ProcedureLabProviderAddressBookMutationResponse = {
  id: number
  addressBook: ProcedureLabProviderAddressBookResponse
}

export type ProcedureOrderCatalogItem = {
  id: number
  parentId?: number | null
  labId?: number | null
  labName?: string | null
  name: string
  code?: string | null
  itemType: string
  procedureTypeName?: string | null
  description?: string | null
  specimen?: string | null
  standardCode?: string | null
  sequence: number
  active: boolean
  childCount: number
}

export type ProcedureOrderCatalogResponse = {
  datasetId: string
  datasetVersion: string
  totalItems: number
  groupCount: number
  orderCount: number
  labProviderCount: number
  items: ProcedureOrderCatalogItem[]
}

export type ProcedureOrderCatalogMutationInput = {
  parentId?: number | null
  labId?: number | null
  name: string
  code?: string | null
  itemType?: string | null
  procedureTypeName?: string | null
  description?: string | null
  specimen?: string | null
  standardCode?: string | null
  sequence?: number | null
  active: boolean
}

export type ProcedureOrderCatalogMutationResponse = {
  id: number
  catalog: ProcedureOrderCatalogResponse
}

export type ProcedureOrderCatalogImportInput = {
  vendorFormat: string
  parentId: number
  labId: number
  csvText: string
}

export type ProcedureOrderCatalogImportItem = {
  id: number
  parentId?: number | null
  code: string
  name: string
  itemType: string
  created: boolean
  reactivated: boolean
}

export type ProcedureOrderCatalogImportResponse = {
  vendorFormat: string
  parentId: number
  labId: number
  importedOrderCount: number
  createdOrderCount: number
  updatedOrderCount: number
  reactivatedOrderCount: number
  deactivatedOrderCount: number
  importedResultCount: number
  createdResultCount: number
  updatedResultCount: number
  reactivatedResultCount: number
  importedItems: ProcedureOrderCatalogImportItem[]
  catalog: ProcedureOrderCatalogResponse
}

export type ProcedureReportReviewQueueFilters = {
  patientId?: string
  providerId?: string | number
  labId?: string | number
  fromDate?: string
  toDate?: string
  limit?: number
}

export type ProcedureSpecimenItem = {
  id: number
  specimenIdentifier?: string | null
  accessionIdentifier?: string | null
  specimenTypeCode?: string | null
  specimenType?: string | null
  collectionMethodCode?: string | null
  collectionMethod?: string | null
  specimenLocationCode?: string | null
  specimenLocation?: string | null
  collectedDate: string
  volumeValue?: number | null
  volumeUnit?: string | null
  conditionCode?: string | null
  specimenCondition?: string | null
  comments?: string | null
}

export type ProcedureOrderItem = {
  id: number
  encounter?: number | null
  providerName?: string | null
  orderDate: string
  orderPriority?: string | null
  code?: string | null
  name?: string | null
  procedureType?: string | null
  diagnosis?: string | null
  instructions?: string | null
  orderStatus?: string | null
  specimens: ProcedureSpecimenItem[]
  reports: ProcedureReportItem[]
}

export type ProcedureOrderCounts = {
  orders: number
  completedOrders: number
  scheduledOrders: number
  reportlessOrders: number
  futureScheduledOrders: number
  specimens: number
  reports: number
  results: number
  finalResults: number
}

export type ProcedureResultsResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  firstName: string
  lastName: string
  counts: ProcedureOrderCounts
  orders: ProcedureOrderItem[]
}

export type ProcedureOrderCreateInput = {
  patientId: string
  providerId?: number | null
  labId?: number | null
  encounterId: number
  dateOrdered: string
  priority: string
  status: string
  procedureCode: string
  procedureName: string
  procedureType: string
  diagnosis: string
  instructions: string
}

export type ProcedureOrderStatusUpdateInput = {
  status: string
}

export type ProcedureOrderTransmitInput = {
  transmittedAt?: string | null
}

export type ProcedureOrderUpdateInput = {
  dateOrdered: string
  priority: string
  status: string
  procedureCode: string
  procedureName: string
  procedureType: string
  diagnosis: string
  instructions: string
}

export type ProcedureReportCreateInput = {
  orderId: number
  dateCollected: string
  dateReport: string
  specimenNumber: string
  reportStatus: string
  reviewStatus: string
  notes: string
}

export type ProcedureReportUpdateInput = {
  dateCollected: string
  dateReport: string
  specimenNumber: string
  reportStatus: string
  reviewStatus: string
  notes: string
}

export type ProcedureReportSignInput = {
  reviewedBy: string
  reviewedAt: string
}

export type ProcedureReportReviewAssignmentInput = {
  assignedTo: string
  assignedAt: string
}

export type ProcedureReportBulkSignInput = ProcedureReportSignInput & {
  reportIds: number[]
}

export type ProcedureReportBulkSignResponse = {
  requestedCount: number
  signedCount: number
  signedReportIds: number[]
  reviewedBy: string
  reviewedAt: string
}

export type ProcedureSpecimenCreateInput = {
  orderId: number
  specimenIdentifier: string
  accessionIdentifier: string
  specimenTypeCode: string
  specimenType: string
  collectionMethodCode: string
  collectionMethod: string
  specimenLocationCode: string
  specimenLocation: string
  collectedDate: string
  volumeValue?: number | null
  volumeUnit: string
  conditionCode: string
  specimenCondition: string
  comments: string
}

export type ProcedureResultCreateInput = {
  reportId: number
  resultCode: string
  resultText: string
  dateTime: string
  facility: string
  units: string
  result: string
  range: string
  abnormal: string
  comments: string
  status: string
}

export type ProcedureResultUpdateInput = {
  resultCode: string
  resultText: string
  dateTime: string
  units: string
  result: string
  range: string
  abnormal: string
  status: string
}

export type ProcedureMutationResponse = {
  id: number
  detail: ProcedureResultsResponse
}

export type BillingLineItem = {
  id: string
  encounter: number
  billingDate: string
  codeType?: string | null
  code?: string | null
  modifier?: string | null
  codeText?: string | null
  fee?: number | null
  justify?: string | null
  units: number
  billed: number
  activity: number
}

export type BillingClaimItem = {
  id: string
  encounter: number
  version: number
  payerId: number
  payerName?: string | null
  payerType: number
  status: number
  statusLabel: string
  billProcess: number
  billTime?: string | null
  processTime?: string | null
  processFile?: string | null
  target?: string | null
  submittedClaim?: string | null
}

export type BillingPaymentItem = {
  activityId: string
  encounter: number
  sequenceNo: number
  sessionId: number
  reference?: string | null
  payerName?: string | null
  payerType: number
  paymentType?: string | null
  paymentMethod?: string | null
  checkDate?: string | null
  depositDate?: string | null
  postDate?: string | null
  postTime: string
  codeType?: string | null
  code?: string | null
  modifier?: string | null
  memo?: string | null
  payAmount: number
  adjustmentAmount: number
  accountCode?: string | null
  reasonCode?: string | null
  payerClaimNumber?: string | null
}

export type BillingAccountSummary = {
  chargeAmount: number
  paymentAmount: number
  adjustmentAmount: number
  balanceAmount: number
}

export type BillingAgingSummary = {
  asOfDate: string
  currentAmount: number
  days31To60Amount: number
  days61To90Amount: number
  over90Amount: number
  totalBalanceAmount: number
}

export type BillingLedgerSummary = {
  entryCount: number
  firstEntryDate?: string | null
  lastEntryDate?: string | null
  chargeAmount: number
  paymentAmount: number
  adjustmentAmount: number
  endingBalanceAmount: number
}

export type BillingLedgerEntry = {
  entryId: string
  entryDate: string
  encounter: number
  entryType: string
  description: string
  code?: string | null
  reference?: string | null
  amount: number
  runningBalanceAmount: number
}

export type BillingStatementSummary = {
  statementStatus: string
  statementPeriodStart: string
  statementPeriodEnd: string
  statementDate: string
  dueDate: string
  recipientName: string
  mailingAddressLine1: string
  mailingAddressLine2: string
  email?: string | null
  phone?: string | null
  openEncounterCount: number
  ledgerEntryCount: number
  oldestOpenAgeDays: number
  oldestOpenDate: string
  chargeAmount: number
  paymentAmount: number
  adjustmentAmount: number
  currentDueAmount: number
  pastDueAmount: number
  balanceDueAmount: number
}

export type BillingStatementLineItem = {
  lineNumber: number
  entryDate: string
  encounter: number
  entryType: string
  description: string
  code?: string | null
  reference?: string | null
  chargeAmount: number
  paymentAmount: number
  refundAmount: number
  adjustmentAmount: number
  balanceAmount: number
}

export type BillingStatementDocument = {
  statementNumber: string
  title: string
  statementStatus: string
  statementDate: string
  dueDate: string
  statementPeriodStart: string
  statementPeriodEnd: string
  recipientName: string
  mailingAddressLine1: string
  mailingAddressLine2: string
  email?: string | null
  phone?: string | null
  paymentInstructions: string
  generatedText: string
  chargeAmount: number
  paymentAmount: number
  adjustmentAmount: number
  currentDueAmount: number
  pastDueAmount: number
  balanceDueAmount: number
  lineItems: BillingStatementLineItem[]
}

export type StatementBatchCandidate = {
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  statementNumber: string
  statementStatus: string
  statementDate: string
  dueDate: string
  balanceDueAmount: number
  pastDueAmount: number
  currentDueAmount: number
  openEncounterCount: number
  ledgerEntryCount: number
  oldestOpenAgeDays: number
  oldestOpenDate: string
  deliveryMethod: string
}

export type StatementBatchResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  candidateCount: number
  totalBalanceAmount: number
  totalPastDueAmount: number
  totalCurrentDueAmount: number
  candidates: StatementBatchCandidate[]
}

export type StatementBatchDeliveryEntry = {
  pubpid: string
  legacyPid: number
  patientDisplayName: string
  statementNumber: string
  statementStatus: string
  statementDate: string
  dueDate: string
  balanceDueAmount: number
  pastDueAmount: number
  currentDueAmount: number
  deliveryMethod: string
  destination: string
  fileName: string
  deliveryStatus: string
}

export type StatementBatchDeliveryResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  deliveryId: string
  preparedAt: string
  candidateCount: number
  includedStatementCount: number
  emailReadyCount: number
  printReadyCount: number
  totalBalanceAmount: number
  totalPastDueAmount: number
  totalCurrentDueAmount: number
  entries: StatementBatchDeliveryEntry[]
}

export type StatementBatchDispatchEntry = {
  dispatchAuditId: string
  pubpid: string
  legacyPid: number
  patientDisplayName: string
  statementNumber: string
  statementStatus: string
  statementDate: string
  dueDate: string
  balanceDueAmount: number
  pastDueAmount: number
  currentDueAmount: number
  deliveryMethod: string
  destination: string
  fileName: string
  queueName: string
  dispatchStatus: string
  externalReference: string
}

export type StatementBatchDispatchResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  deliveryId: string
  dispatchId: string
  dispatchedAt: string
  candidateCount: number
  dispatchedStatementCount: number
  emailQueueCount: number
  printQueueCount: number
  totalBalanceAmount: number
  totalPastDueAmount: number
  totalCurrentDueAmount: number
  entries: StatementBatchDispatchEntry[]
}

export type StatementDeliveryAuditHistoryEntry = StatementBatchDispatchEntry & {
  deliveryId: string
  dispatchId: string
  dispatchedAt: string
  createdAt: string
}

export type StatementDeliveryAuditHistoryResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  eventCount: number
  entries: StatementDeliveryAuditHistoryEntry[]
}

export type StatementPortalDeliveryEntry = {
  documentId: number
  documentKey: string
  pubpid: string
  legacyPid: number
  patientDisplayName: string
  portalUsername: string
  statementNumber: string
  statementStatus: string
  statementDate: string
  dueDate: string
  balanceDueAmount: number
  pastDueAmount: number
  currentDueAmount: number
  categoryName: string
  documentName: string
  fileName: string
  deliveryStatus: string
}

export type StatementPortalDeliveryResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  portalDeliveryId: string
  deliveredAt: string
  candidateCount: number
  portalEligibleCount: number
  deliveredDocumentCount: number
  skippedCount: number
  totalBalanceAmount: number
  totalPastDueAmount: number
  totalCurrentDueAmount: number
  entries: StatementPortalDeliveryEntry[]
}

export type StatementEmailOutboxEntry = {
  outboxMessageId: string
  pubpid: string
  legacyPid: number
  patientDisplayName: string
  statementNumber: string
  statementStatus: string
  statementDate: string
  dueDate: string
  balanceDueAmount: number
  pastDueAmount: number
  currentDueAmount: number
  toEmail: string
  fromEmail: string
  subject: string
  bodyPreview: string
  attachmentFileName: string
  queueName: string
  deliveryStatus: string
  externalReference: string
}

export type StatementEmailOutboxResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  outboxBatchId: string
  queuedAt: string
  candidateCount: number
  emailEligibleCount: number
  queuedMessageCount: number
  skippedCount: number
  totalBalanceAmount: number
  totalPastDueAmount: number
  totalCurrentDueAmount: number
  entries: StatementEmailOutboxEntry[]
}

export type CollectionsWorkQueueItem = {
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  statementNumber: string
  statementDate: string
  dueDate: string
  balanceDueAmount: number
  pastDueAmount: number
  over90Amount: number
  currentDueAmount: number
  openEncounterCount: number
  ledgerEntryCount: number
  oldestOpenAgeDays: number
  oldestOpenDate: string
  collectionTier: string
  recommendedAction: string
  contactMethod: string
  email?: string | null
  phone?: string | null
}

export type CollectionsWorkQueueResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  accountCount: number
  highPriorityCount: number
  totalBalanceAmount: number
  totalPastDueAmount: number
  totalOver90Amount: number
  items: CollectionsWorkQueueItem[]
}

export type CollectionsFollowUpCreateInput = {
  patientId: string
  assignedTo?: string | null
  action?: string | null
  note?: string | null
}

export type CollectionsFollowUpTask = {
  id: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  statementNumber: string
  title: string
  body: string
  status: string
  assignedTo: string
  action: string
  collectionTier: string
  pastDueAmount: number
  over90Amount: number
}

export type CollectionsFollowUpMutationResponse = {
  id: string
  task: CollectionsFollowUpTask
  detail: PatientBillingResponse
}

export type BillingEncounterItem = {
  id: number
  encounter: number
  date: string
  reason?: string | null
  diagnosisCode?: string | null
  diagnosisText?: string | null
  providerName?: string | null
  facilityName?: string | null
  totalFee: number
  paymentAmount: number
  adjustmentAmount: number
  balanceAmount: number
  ageDays: number
  agingBucket: string
  lines: BillingLineItem[]
  claims: BillingClaimItem[]
  payments: BillingPaymentItem[]
}

export type PatientBillingResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  firstName: string
  lastName: string
  accountSummary: BillingAccountSummary
  agingSummary: BillingAgingSummary
  ledgerSummary: BillingLedgerSummary
  statementSummary: BillingStatementSummary
  statementDocument: BillingStatementDocument
  ledgerEntries: BillingLedgerEntry[]
  encounters: BillingEncounterItem[]
}

export type BillingLineCreateInput = {
  patientId: string
  providerId?: number | null
  encounter: number
  billingDate: string
  codeType: string
  code: string
  modifier?: string | null
  codeText: string
  fee: number
  units: number
  justify: string
}

export type BillingLineUpdateInput = {
  codeText: string
  modifier?: string | null
  fee: number
  units: number
  justify: string
}

export type BillingLineStatusUpdateInput = {
  billed: number
  activity: number
}

export type BillingLineMutationResponse = {
  id: string
  detail: PatientBillingResponse
}

export type BillingChargeTemplate = {
  id: string
  label: string
  code: string
  modifier: string
  description: string
  fee: string
  units: number
  justify: string
}

export type BillingClaimCreateInput = {
  patientId: string
  encounter: number
  payerId: number
  payerName?: string | null
  payerType: number
  status: number
  billProcess: number
  billTime?: string | null
  processTime?: string | null
  processFile?: string | null
  target?: string | null
  x12PartnerId?: number | null
  submittedClaim?: string | null
}

export type BillingClaimStatusUpdateInput = {
  status: number
  billProcess: number
  processTime?: string | null
  processFile?: string | null
  target?: string | null
  x12PartnerId?: number | null
  submittedClaim?: string | null
}

export type BillingClaimMutationResponse = {
  id: string
  detail: PatientBillingResponse
}

export type BillingPatientPaymentCreateInput = {
  patientId: string
  encounter: number
  reference: string
  postDate: string
  checkDate?: string | null
  depositDate?: string | null
  paymentMethod: string
  codeType?: string | null
  code?: string | null
  modifier?: string | null
  memo: string
  payAmount: number
}

export type BillingPatientRefundCreateInput = {
  patientId: string
  encounter: number
  reference: string
  postDate: string
  checkDate?: string | null
  depositDate?: string | null
  paymentMethod: string
  codeType?: string | null
  code?: string | null
  modifier?: string | null
  memo: string
  refundAmount: number
}

export type BillingInsurancePaymentCreateInput = {
  patientId: string
  encounter: number
  payerId: number
  payerName: string
  reference: string
  postDate: string
  checkDate?: string | null
  depositDate?: string | null
  paymentMethod: string
  codeType?: string | null
  code?: string | null
  modifier?: string | null
  memo: string
  payAmount: number
  adjustmentAmount: number
  reasonCode: string
  payerClaimNumber?: string | null
}

export type BillingInsuranceReversalCreateInput = {
  patientId: string
  encounter: number
  payerId: number
  payerName: string
  reference: string
  postDate: string
  checkDate?: string | null
  depositDate?: string | null
  paymentMethod: string
  codeType?: string | null
  code?: string | null
  modifier?: string | null
  memo: string
  reversalAmount: number
  payerClaimNumber?: string | null
}

export type BillingAdjustmentReversalCreateInput = {
  patientId: string
  encounter: number
  payerId: number
  payerName: string
  reference: string
  postDate: string
  checkDate?: string | null
  depositDate?: string | null
  paymentMethod: string
  codeType?: string | null
  code?: string | null
  modifier?: string | null
  memo: string
  adjustmentAmount: number
  payerClaimNumber?: string | null
}

export type BillingEobBatchImportInput = {
  patientId: string
}

export type BillingPaymentMutationResponse = {
  id: string
  sessionId: number
  detail: PatientBillingResponse
}

export type BillingEobBatchImportResponse = {
  ids: string[]
  sessionIds: number[]
  detail: PatientBillingResponse
}

export type AdministrationDirectoryCounts = {
  users: number
  providers: number
  calendarUsers: number
  facilities: number
  accessGroups: number
  accessPermissions: number
  accessGroupPermissions: number
  accessUserMemberships: number
  waitingPortalAudits: number
  waitingProfileReviews: number
}

export type AdministrationUserItem = {
  id: number
  username: string
  firstName: string
  lastName: string
  displayName: string
  role: string
  authorized: boolean
  active: boolean
  calendar: boolean
  facilityId?: number | null
  facilityName?: string | null
  email?: string | null
  npi?: string | null
}

export type AdministrationUserMutationInput = {
  username: string
  firstName: string
  lastName: string
  role: string
  calendar?: boolean | null
  facilityId?: number | null
  email?: string | null
  npi?: string | null
  active?: boolean | null
}

export type AdministrationFacilityItem = {
  id: number
  code: string
  name: string
  active: boolean
  phone?: string | null
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  color?: string | null
}

export type AdministrationFacilityMutationInput = {
  code: string
  name: string
  phone?: string | null
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  color?: string | null
  active?: boolean | null
}

export type AdministrationAccessGroupItem = {
  id: number
  value: string
  name: string
  parentId?: number | null
  permissionCount: number
}

export type AdministrationAccessPermissionItem = {
  sectionValue: string
  value: string
  name: string
}

export type AdministrationAccessGroupPermissionItem = {
  groupValue: string
  sectionValue: string
  permissionValue: string
  permissionName: string
  returnValue: string
}

export type AdministrationAccessUserMembershipItem = {
  userValue: string
  userName: string
  groupValue: string
  groupName: string
  staffId?: number | null
}

export type AdministrationAccessPermissionMutationInput = {
  groupValue: string
  sectionValue: string
  permissionValue: string
  returnValue: string
}

export type AdministrationAccessUserMembershipMutationInput = {
  userValue: string
  groupValue: string
}

export type AdministrationAccessControlSummary = {
  groups: AdministrationAccessGroupItem[]
  permissions: AdministrationAccessPermissionItem[]
  groupPermissions: AdministrationAccessGroupPermissionItem[]
  userMemberships: AdministrationAccessUserMembershipItem[]
}

export type AdministrationPortalProfileReviewRequest = {
  id: string
  requestedAt: string
  patientId: string
  legacyPid: number
  pubpid: string
  firstName: string
  middleName: string
  lastName: string
  patientName: string
  activity: string
  requireAudit: number
  pendingAction: string
  actionTaken: string
  status: string
  narrative: string
  tableAction: string
  actionUser?: string | null
  actionTakenAt?: string | null
  checksum: string
  requestedDemographics: PatientPortalProfileDemographics
}

export type AdministrationPortalActivitySummary = {
  waitingAuditCount: number
  waitingProfileReviewCount: number
  profileReviewRequests: AdministrationPortalProfileReviewRequest[]
}

export type AdministrationPortalProfileReviewMutationResponse = {
  id: string
  patientId: string
  legacyPid: number
  status: string
  pendingAction: string
  actionTaken: string
  narrative: string
  tableAction: string
  actionUser: string
  actionTakenAt: string
  requestedDemographics: PatientPortalProfileDemographics
  detail: AdministrationDirectoryResponse
}

export type AdministrationDirectoryResponse = {
  datasetId: string
  datasetVersion: string
  counts: AdministrationDirectoryCounts
  users: AdministrationUserItem[]
  facilities: AdministrationFacilityItem[]
  accessControl: AdministrationAccessControlSummary
  portalActivity: AdministrationPortalActivitySummary
}

export type AdministrationFacilityMutationResponse = {
  id: number
  detail: AdministrationDirectoryResponse
}

export type AdministrationUserMutationResponse = {
  id: number
  detail: AdministrationDirectoryResponse
}

export type AdministrationAccessPermissionMutationResponse = {
  groupValue: string
  sectionValue: string
  permissionValue: string
  returnValue?: string | null
  detail: AdministrationDirectoryResponse
}

export type AdministrationAccessUserMembershipMutationResponse = {
  userValue: string
  groupValue: string
  detail: AdministrationDirectoryResponse
}

export type OperationalReportCounts = {
  patients: number
  portalPatients: number
  appointments: number
  futureAppointments: number
  currentYearAppointments: number
  encounters: number
  currentYearEncounters: number
  billingLines: number
  billingTotal: number
  labReports: number
  patientDocuments: number
  messages: number
  newMessages: number
  doneMessages: number
  facilities: number
  providers: number
}

export type ProviderActivityReportItem = {
  username: string
  firstName: string
  lastName: string
  displayName: string
  encounters: number
  billingLines: number
  billingTotal: number
}

export type FacilityActivityReportItem = {
  code: string
  name: string
  appointments: number
  encounters: number
  billingLines: number
  billingTotal: number
}

export type ClinicalConditionReportItem = {
  title: string
  diagnosis: string
  patients: number
}

export type OperationalReportsResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  currentYear: number
  counts: OperationalReportCounts
  providerActivity: ProviderActivityReportItem[]
  facilityActivity: FacilityActivityReportItem[]
  clinicalConditions: ClinicalConditionReportItem[]
}

export type InventoryLot = {
  lotId: number
  facilityCode: string
  facilityName: string
  lotNumber: string
  expirationDate?: string | null
  quantityOnHand: number
  unitCost: number
  status: string
  expiryStatus?: 'not-tracked' | 'current' | 'expiring' | 'expired' | null
}

export type InventoryFacility = {
  facilityId: number
  code: string
  name: string
}

export type InventoryItem = {
  itemId: number
  itemCode: string
  name: string
  category: string
  unit: string
  reorderPoint: number
  preferredQuantity: number
  quantityOnHand: number
  inventoryValue: number
  belowReorderPoint: boolean
  medicationLink?: InventoryMedicationLink | null
  lots: InventoryLot[]
}

export type InventoryMedicationLink = {
  itemId: number
  rxNormCode: string
  drugName: string
  displayName: string
  linkedBy: string
  linkedAt: string
}

export type InventoryMedicationCatalogItem = {
  rxNormCode: string
  drugName: string
  displayName: string
  form: string
  strength: string
  route: string
}

export type InventoryMedicationLinkUpdateInput = { rxNormCode: string }

export type InventoryPrescriptionDispenseInput = {
  prescriptionId: string
  quantity: number
  fee: number
  saleDate?: string | null
  notes?: string | null
}

export type InventoryPrescriptionDispenseResponse = {
  prescriptionId: string
  itemId: number
  patientId: string
  encounter: number
  rxNormCode: string
  sale: InventoryPatientSaleResponse
}

export type InventoryTransactionItem = {
  transactionId: string
  lotId: number
  itemCode: string
  itemName: string
  facilityCode: string
  transactionType: string
  quantityDelta: number
  reason?: string | null
  performedBy: string
  occurredAt: string
  transferId?: string | null
  counterpartyFacilityCode?: string | null
  receiptId?: string | null
  receiptReference?: string | null
}

export type InventoryResponse = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  summary: {
    activeItems: number
    activeLots: number
    belowReorderPoint: number
    expiredLots: number
    expiringWithin90Days: number
  inventoryValue: number
  }
  facilities: InventoryFacility[]
  items: InventoryItem[]
  recentTransactions: InventoryTransactionItem[]
}

export type InventoryTransactionCreateInput = {
  lotId: number
  transactionType: string
  quantity: number
  reason?: string | null
}

export type InventoryTransferCreateInput = {
  sourceLotId: number
  destinationFacilityId: number
  quantity: number
  reason?: string | null
}

export type InventoryMutationResponse = {
  transaction: InventoryTransactionItem
  lot: InventoryLot
  itemQuantityOnHand: number
  belowReorderPoint: boolean
  counterpartyLot?: InventoryLot | null
  transferId?: string | null
}

export type InventoryLotMetadataUpdateInput = {
  lotNumber: string
  expirationDate?: string | null
}

export type InventoryLotMetadataUpdateResponse = {
  auditId: string
  lot: InventoryLot
  changedBy: string
  changedAt: string
}

export type InventoryLotMetadataAuditItem = {
  auditId: string
  priorLotNumber: string
  newLotNumber: string
  priorExpirationDate?: string | null
  newExpirationDate?: string | null
  changedBy: string
  changedAt: string
}

export type InventoryLotDestructionInput = {
  destructionDate?: string | null
  method?: string | null
  witness?: string | null
  notes?: string | null
}

export type InventoryLotDestructionResponse = {
  destructionId: string
  lot: InventoryLot
  destructionDate: string
  method?: string | null
  witness?: string | null
  notes?: string | null
  destroyedBy: string
  recordedAt: string
}

export type InventoryExpiryDispositionInput = { disposition: 'quarantine' | 'return' | 'destroy'; notes: string; method?: string | null; witness?: string | null }
export type InventoryExpiryDispositionResponse = { dispositionId: string; disposition: string; lot: InventoryLot; quantityAffected: number; notes: string; method?: string | null; witness?: string | null; disposedBy: string; disposedAt: string; destructionId?: string | null }

export type InventoryPatientSaleCreateInput = {
  lotId: number
  patientId: string
  encounter: number
  saleDate?: string | null
  quantity: number
  fee: number
  notes?: string | null
}

export type InventoryPatientSaleResponse = {
  saleId: string
  patientId: string
  encounter: number
  saleDate: string
  quantity: number
  fee: number
  notes?: string | null
  soldBy: string
  soldAt: string
  inventoryMutation: InventoryMutationResponse
}

export type InventoryPatientSaleAllocationCreateInput = Omit<InventoryPatientSaleCreateInput, 'lotId'> & { itemId: number }
export type InventoryPatientSaleAllocationResponse = {
  saleBatchId: string
  itemId: number
  patientId: string
  encounter: number
  saleDate: string
  quantity: number
  fee: number
  allocations: Array<{ saleId: string; lotId: number; lotNumber: string; quantity: number; fee: number; transactionId: string }>
}

export type InventoryActivityReportResponse = {
  datasetId: string
  datasetVersion: string
  fromDate?: string | null
  toDate?: string | null
  facilityId?: number | null
  totalEntries: number
  entries: InventoryTransactionItem[]
}

export type InventoryVendor = {
  vendorId: string
  name: string
  contactName?: string | null
  phone?: string | null
  email?: string | null
  active: boolean
}

export type InventoryVendorListResponse = {
  vendors: InventoryVendor[]
}

export type InventoryVendorCreateInput = {
  name: string
  contactName?: string | null
  phone?: string | null
  email?: string | null
}

export type InventoryPurchaseReceiptCreateInput = {
  vendorId: string
  facilityId: number
  itemId: number
  lotNumber: string
  expirationDate?: string | null
  quantity: number
  unitCost: number
  referenceNumber?: string | null
  notes: string
  requisitionId?: string | null
}

export type InventoryPurchaseReceiptReconciliation = {
  reconciliationId: string
  requisitionId: string
  requisitionLineId: string
  receivedQuantity: number
  reconciledBy: string
  reconciledAt: string
}

export type InventoryPurchaseReceiptResponse = {
  receiptId: string
  vendor: InventoryVendor
  facilityCode: string
  facilityName: string
  referenceNumber?: string | null
  receivedAt: string
  receivedBy: string
  notes: string
  lot: InventoryLot
  transaction: InventoryTransactionItem
  itemQuantityOnHand: number
  belowReorderPoint: boolean
  requisitionReconciliation?: InventoryPurchaseReceiptReconciliation | null
}

export type InventoryPurchaseRequisitionLineInput = { itemId: number; quantity: number }
export type InventoryPurchaseRequisitionCreateInput = {
  facilityId: number
  vendorId?: string | null
  notes?: string | null
  lines: InventoryPurchaseRequisitionLineInput[]
}
export type InventoryPurchaseRequisitionDecisionInput = { notes?: string | null }
export type InventoryPurchaseRequisition = {
  requisitionId: string
  facilityId: number
  facilityCode: string
  facilityName: string
  vendorId?: string | null
  vendorName?: string | null
  status: 'draft' | 'submitted' | 'approved' | 'rejected'
  notes?: string | null
  requestedBy: string
  requestedAt: string
  submittedBy?: string | null
  submittedAt?: string | null
  decidedBy?: string | null
  decidedAt?: string | null
  decisionNotes?: string | null
  receiptStatus: 'pending' | 'partial' | 'complete'
  lines: Array<{ requisitionLineId: string; itemId: number; itemCode: string; itemName: string; requestedQuantity: number; receivedQuantity: number; outstandingQuantity: number; unit: string }>
  events: Array<{ eventId: string; action: string; note?: string | null; actor: string; occurredAt: string }>
}

export type InventoryCountReconciliationCreateInput = {
  lotId: number
  countedQuantity: number
  notes: string
}

export type InventoryCountReconciliationResponse = {
  reconciliationId: string
  lotId: number
  expectedQuantity: number
  countedQuantity: number
  quantityDelta: number
  notes: string
  countedBy: string
  countedAt: string
  lot: InventoryLot
  transaction: InventoryTransactionItem
  itemQuantityOnHand: number
  belowReorderPoint: boolean
}

export type AuthLoginInput = {
  username: string
  password: string
}

export type AuthLoginResponse = {
  authenticated: boolean
  username: string
  displayName: string
  role: string
  staffId?: number | null
  failureReason?: string | null
  sessionId?: string | null
  sessionCreatedAt?: string | null
  sessionExpiresAt?: string | null
}

export type AuthSessionResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  displayName: string
  role: string
  staffId?: number | null
  createdAt?: string | null
  lastSeenAt?: string | null
  expiresAt?: string | null
  endedAt?: string | null
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalLoginInput = {
  username: string
  password: string
}

export type PatientPortalLoginResponse = {
  authenticated: boolean
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  failureReason?: string | null
  sessionId?: string | null
  sessionCreatedAt?: string | null
  sessionExpiresAt?: string | null
  sessionSource: string
}

export type PatientPortalSessionResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  createdAt?: string | null
  lastSeenAt?: string | null
  expiresAt?: string | null
  endedAt?: string | null
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalHomeMessageSummary = {
  totalMessages: number
  newMessages: number
  doneMessages: number
  latestMessageTitle?: string | null
  latestMessageDate?: string | null
}

export type PatientPortalHomeAppointmentSummary = {
  id: string
  date: string
  startTime: string
  title: string
  status?: string | null
  categoryId?: number | null
  categoryName?: string | null
  providerName?: string | null
  facilityName?: string | null
  comments?: string | null
}

export type PatientPortalHomeImmunizationSummary = {
  id: number
  administeredDate?: string | null
  administeredFormatted?: string | null
  immunizationId?: number | null
  cvxCode?: string | null
  codeText: string
  note?: string | null
  completionStatus?: string | null
  addedErroneously: number
}

export type PatientPortalHomeSummaryResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  messages: PatientPortalHomeMessageSummary
  upcomingAppointmentCount: number
  upcomingAppointments: PatientPortalHomeAppointmentSummary[]
  immunizationCount: number
  immunizations: PatientPortalHomeImmunizationSummary[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalProfileDemographics = {
  firstName: string
  lastName: string
  preferredName?: string | null
  dateOfBirth?: string | null
  sex?: string | null
  email?: string | null
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  phoneHome?: string | null
  phoneCell?: string | null
  phoneContact?: string | null
  hipaaAllowSms?: string | null
  hipaaAllowEmail?: string | null
  contactRelationship?: string | null
  motherName?: string | null
  guardianName?: string | null
  guardianRelationship?: string | null
  guardianPhone?: string | null
  guardianEmail?: string | null
}

export type PatientPortalProfileInsurance = {
  type: string
  provider?: string | null
  planName?: string | null
  policyNumber?: string | null
  groupNumber?: string | null
  subscriberFirstName?: string | null
  subscriberLastName?: string | null
  subscriberName?: string | null
  subscriberRelationship?: string | null
  subscriberDateOfBirth?: string | null
}

export type PatientPortalProfileChangeRequest = {
  id: number
  status: string
  pendingAction: string
  narrative: string
  requestedAt: string
  updatedAt?: string | null
  demographics: PatientPortalProfileDemographics
}

export type PatientPortalProfileChangeInput = {
  email?: string | null
  phoneHome?: string | null
  phoneCell?: string | null
  hipaaAllowSms?: string | null
  hipaaAllowEmail?: string | null
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
}

export type PatientPortalProfileResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  hasPendingProfileChanges: boolean
  demographics: PatientPortalProfileDemographics
  insuranceCount: number
  insurance: PatientPortalProfileInsurance[]
  pendingChange?: PatientPortalProfileChangeRequest | null
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalAppointmentsResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  upcomingAppointmentCount: number
  upcomingAppointments: PatientPortalHomeAppointmentSummary[]
  pastAppointmentCount: number
  pastAppointments: PatientPortalHomeAppointmentSummary[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalProblemItem = {
  id: string
  title: string
  reportedDate?: string | null
  startDate?: string | null
  endDate?: string | null
}

export type PatientPortalAllergyItem = {
  id: string
  title: string
  reportedDate?: string | null
  startDate?: string | null
  endDate?: string | null
  referredBy?: string | null
  reaction?: string | null
  severity?: string | null
}

export type PatientPortalMedicationItem = {
  id: string
  title: string
  startDate?: string | null
  modifiedDate?: string | null
  endDate?: string | null
}

export type PatientPortalPrescriptionItem = {
  id: string
  drug: string
  startDate?: string | null
  modifiedDate?: string | null
  endDate?: string | null
  dosage?: string | null
  quantity?: string | null
  route?: string | null
  note?: string | null
}

export type PatientPortalClinicalSummaryResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  problemCount: number
  problems: PatientPortalProblemItem[]
  allergyCount: number
  allergies: PatientPortalAllergyItem[]
  medicationCount: number
  medications: PatientPortalMedicationItem[]
  prescriptionCount: number
  prescriptions: PatientPortalPrescriptionItem[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalPrescriptionRefillRequestInput = {
  requestDate?: string | null
  note?: string | null
}

export type PatientPortalLabResultItem = {
  id: string
  resultCode?: string | null
  resultName: string
  abnormal?: string | null
  value?: string | null
  range?: string | null
  units?: string | null
  resultStatus?: string | null
}

export type PatientPortalLabReportItem = {
  id: string
  dateCollected?: string | null
  reportDate?: string | null
  specimenNumber?: string | null
  reportStatus?: string | null
  reviewStatus?: string | null
  resultCount: number
  results: PatientPortalLabResultItem[]
}

export type PatientPortalLabOrderItem = {
  id: string
  orderDate: string
  procedureCode?: string | null
  procedureName: string
  orderStatus?: string | null
  reportCount: number
  resultCount: number
  reports: PatientPortalLabReportItem[]
}

export type PatientPortalLabResultsResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  orderCount: number
  reportCount: number
  resultCount: number
  orders: PatientPortalLabOrderItem[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalMedicalReportSection = {
  id: string
  label: string
  group: string
  selected: boolean
}

export type PatientPortalMedicalReportIssue = {
  id: string
  type: string
  typeLabel: string
  title: string
  beginDate?: string | null
  endDate?: string | null
  status: string
  encounterIds: number[]
}

export type PatientPortalMedicalReportEncounterForm = {
  id: string
  formDirectory: string
  display: string
  encounter: number
}

export type PatientPortalMedicalReportEncounter = {
  encounter: number
  date: string
  display: string
  reason?: string | null
  formCount: number
  forms: PatientPortalMedicalReportEncounterForm[]
}

export type PatientPortalMedicalReportProcedureOrder = {
  id: string
  encounter: number
  orderDate: string
  encounterDate?: string | null
  procedureCode?: string | null
  procedureName: string
  diagnosis?: string | null
  orderStatus?: string | null
  reportCount: number
  resultCount: number
  resultNames: string[]
}

export type PatientPortalGeneratedMedicalReport = {
  title: string
  includedSectionIds: string[]
  includedProcedureOrderIds: string[]
  includedEncounterFormIds: string[]
  templateMetadata: PatientPortalGeneratedMedicalReportTemplateMetadata
  packageMetadata: PatientPortalGeneratedMedicalReportPackageMetadata
  summaryLineCount: number
  summaryLines: string[]
}

export type PatientPortalMedicalReportGenerationInput = {
  sectionIds?: string[]
  procedureOrderIds?: string[]
  issueIds?: string[]
  encounterFormIds?: string[]
}

export type PatientPortalGeneratedMedicalReportSection = {
  id: string
  title: string
  lineCount: number
  lines: string[]
}

export type PatientPortalGeneratedMedicalReportTemplateMetadata = {
  facilityName: string
  facilityStreet: string
  facilityCityStatePostal: string
  facilityPhone: string
  printablePatientName: string
  patientHeaderLine: string
  generatedOnLabel: string
  signatureLineAvailable: boolean
}

export type PatientPortalGeneratedMedicalReportPackageMetadata = {
  fileName: string
  contentType: string
  entryNames: string[]
  manifestAvailable: boolean
  pdfAvailable: boolean
  summaryAvailable: boolean
}

export type PatientPortalGeneratedMedicalReportAuditEvent = {
  id: number
  eventType: string
  eventLabel: string
  eventAt: string
  reportTitle: string
  generatedOn: string
  artifactName?: string | null
  artifactContentType?: string | null
  includedSectionIds: string[]
  includedIssueIds: string[]
  includedEncounterFormIds: string[]
  includedProcedureOrderIds: string[]
  summary: string
  eventSource: string
}

export type PatientPortalGeneratedMedicalReportResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  title: string
  generatedOn: string
  templateMetadata: PatientPortalGeneratedMedicalReportTemplateMetadata
  includedSectionIds: string[]
  includedProcedureOrderIds: string[]
  includedIssueIds: string[]
  includedEncounterFormIds: string[]
  printableVersionAvailable: boolean
  pdfDownloadAvailable: boolean
  packageDownloadAvailable: boolean
  packageMetadata: PatientPortalGeneratedMedicalReportPackageMetadata
  reportSectionCount: number
  reportSections: PatientPortalGeneratedMedicalReportSection[]
  summaryLineCount: number
  summaryLines: string[]
  auditEventCount: number
  auditEvents: PatientPortalGeneratedMedicalReportAuditEvent[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalGeneratedMedicalReportAuditResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  auditEventCount: number
  auditEvents: PatientPortalGeneratedMedicalReportAuditEvent[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalMedicalReportResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  sectionCount: number
  selectedSectionCount: number
  sections: PatientPortalMedicalReportSection[]
  issueCount: number
  issues: PatientPortalMedicalReportIssue[]
  encounterCount: number
  encounters: PatientPortalMedicalReportEncounter[]
  procedureOrderCount: number
  procedureOrders: PatientPortalMedicalReportProcedureOrder[]
  reportPreview: PatientPortalGeneratedMedicalReport
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalAppointmentCategoryOption = {
  id: number
  name: string
  constantId: string
  durationMinutes: number
}

export type PatientPortalAppointmentProviderOption = {
  id: number
  username: string
  displayName: string
  facilityId?: number | null
  facilityName?: string | null
}

export type PatientPortalAppointmentFacilityOption = {
  id: number
  name: string
  code?: string | null
}

export type PatientPortalAppointmentRequestDefaults = {
  categoryId?: number | null
  providerId?: number | null
  facilityId?: number | null
  durationMinutes: number
  date: string
  startTime: string
}

export type PatientPortalAppointmentRequestOptionsResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  categories: PatientPortalAppointmentCategoryOption[]
  providers: PatientPortalAppointmentProviderOption[]
  facilities: PatientPortalAppointmentFacilityOption[]
  defaults: PatientPortalAppointmentRequestDefaults
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalAppointmentRequestInput = {
  providerId?: number | null
  facilityId?: number | null
  categoryId?: number | null
  date: string
  startTime: string
  durationMinutes: number
  reason?: string | null
}

export type PatientPortalAppointmentReminder = {
  id: string
  title: string
  body: string
  assignedTo: string
  status: string
}

export type PatientPortalAppointmentRequestResponse = {
  authenticated: boolean
  created: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  appointment?: PatientPortalHomeAppointmentSummary | null
  reminder?: PatientPortalAppointmentReminder | null
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalMessageItem = {
  id: string
  type: string
  date: string
  title: string
  body: string
  status: string
  assignedTo: string
  senderId: string
  senderName: string
  recipientId: string
  recipientName: string
  mailChain: number
  replyMailChain: number
  portalRelation?: string | null
  isEncrypted: boolean
  attachmentCount: number
  attachments: PatientPortalMessageAttachment[]
}

export type PatientPortalMessageAttachment = {
  id: string
  fileName: string
  contentType: string
  sizeBytes?: number | null
  source: string
}

export type PatientPortalMessageAttachmentSubmission = {
  fileName?: string | null
  contentType?: string | null
  sizeBytes?: number | null
}

export type PatientPortalMessagesResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  messageCount: number
  messages: PatientPortalMessageItem[]
  sentMessageCount: number
  sentMessages: PatientPortalMessageItem[]
  allMessageCount: number
  allMessages: PatientPortalMessageItem[]
  deletedMessageCount: number
  deletedMessages: PatientPortalMessageItem[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalMessageRecipientOption = {
  id: string
  displayName: string
  type: string
  active: boolean
  fallback: boolean
}

export type PatientPortalMessageRecipientsResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  recipientCount: number
  recipients: PatientPortalMessageRecipientOption[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalMessageSubjectOption = {
  value: string
  label: string
  default: boolean
}

export type PatientPortalMessageComposeOptionsResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  defaultSubject: string
  subjectCount: number
  subjectOptions: PatientPortalMessageSubjectOption[]
  recipientCount: number
  recipients: PatientPortalMessageRecipientOption[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalDocumentItem = {
  id: number
  documentKey: string
  categoryId: number
  categoryName: string
  displayPath: string
  name: string
  docDate: string
  uploadedAt: string
  mimetype?: string | null
  fileName: string
  sizeBytes?: number | null
  storageMethod?: string | null
  canDownload: boolean
}

export type PatientPortalDocumentCategory = {
  categoryId: number
  categoryName: string
  displayPath: string
  documentCount: number
  documents: PatientPortalDocumentItem[]
}

export type PatientPortalDocumentsResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  documentCount: number
  categories: PatientPortalDocumentCategory[]
  documents: PatientPortalDocumentItem[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalDocumentsDownloadInput = {
  documentIds: number[]
}

export type PatientPortalMessageThreadResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  messageId: string
  threadId: number
  anchorMessage?: PatientPortalMessageItem | null
  threadMessageCount: number
  threadMessages: PatientPortalMessageItem[]
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalComposeMessageInput = {
  recipientId?: string | null
  title: string
  body: string
  attachments?: PatientPortalMessageAttachmentSubmission[] | null
}

export type PatientPortalComposeMessageResponse = {
  authenticated: boolean
  created: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  recipientId: string
  recipientName: string
  sentMessage?: PatientPortalMessageItem | null
  recipientMessage?: PatientPortalMessageItem | null
  messageCount: number
  sentMessageCount: number
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalReplyMessageInput = {
  body: string
  attachments?: PatientPortalMessageAttachmentSubmission[] | null
}

export type PatientPortalReplyMessageResponse = {
  authenticated: boolean
  created: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  originalMessageId: string
  originalMessage?: PatientPortalMessageItem | null
  sentMessage?: PatientPortalMessageItem | null
  recipientMessage?: PatientPortalMessageItem | null
  messageCount: number
  sentMessageCount: number
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalForwardMessageInput = {
  body: string
  assignedTo?: string | null
}

export type PatientPortalForwardMessageResponse = {
  authenticated: boolean
  forwarded: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  originalMessageId: string
  originalMessage?: PatientPortalMessageItem | null
  forwardedPatientMessage?: PatientMessageItem | null
  messageCount: number
  sentMessageCount: number
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalReadMessageResponse = {
  authenticated: boolean
  markedRead: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  messageId: string
  message?: PatientPortalMessageItem | null
  messageCount: number
  sentMessageCount: number
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalDeleteMessageResponse = {
  authenticated: boolean
  deleted: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  messageId: string
  deletedMessage?: PatientPortalMessageItem | null
  deletedMessageCount: number
  messageCount: number
  sentMessageCount: number
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalArchiveMessagesInput = {
  messageIds: number[]
}

export type PatientPortalArchiveMessagesResponse = {
  authenticated: boolean
  archived: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  messageIds: string[]
  archivedMessages: PatientPortalMessageItem[]
  archivedMessageCount: number
  messageCount: number
  sentMessageCount: number
  failureReason?: string | null
  sessionSource: string
}

export type PatientPortalMessageAuditEvent = {
  id: number
  eventType: string
  eventLabel: string
  eventAt: string
  messageId: string
  relatedMessageIds: string[]
  messageTitle: string
  messageStatus: string
  recipientId?: string | null
  recipientName?: string | null
  threadId: number
  archivedMessageCount: number
  summary: string
  eventSource: string
}

export type PatientPortalMessageAuditResponse = {
  authenticated: boolean
  sessionId?: string | null
  username: string
  portalUsername: string
  canonicalId: string
  legacyPid?: number | null
  pubpid: string
  displayName: string
  datasetId: string
  datasetVersion: string
  asOfDate: string
  auditEventCount: number
  auditEvents: PatientPortalMessageAuditEvent[]
  failureReason?: string | null
  sessionSource: string
}

export type AuthAuditEventItem = {
  id: number
  occurredAt: string
  event: string
  username: string
  success: boolean
  sourceIp?: string | null
  comment: string
  failureReason?: string | null
  logSource: string
}

export type AuthAuditResponse = {
  totalEvents: number
  successfulLogins: number
  failedLogins: number
  events: AuthAuditEventItem[]
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5001'

function buildLegacyEhrSessionHeaders(sessionId?: string | null, contentType?: string): HeadersInit {
  const headers: Record<string, string> = {}
  if (contentType) {
    headers['content-type'] = contentType
  }
  if (sessionId) {
    headers['X-Legacy EHR-Session'] = sessionId
  }
  return headers
}

function adminApiError(
  action: string,
  status: number,
  forbiddenRequirement = 'ACL Administration access',
  sessionRequirement = 'an active admin session',
) {
  if (status === 401) {
    return `${action} requires ${sessionRequirement}.`
  }
  if (status === 403) {
    return `${action} requires ${forbiddenRequirement}.`
  }

  return `${action} failed with ${status}`
}

export async function login(input: AuthLoginInput, signal?: AbortSignal): Promise<AuthLoginResponse> {
  const response = await fetch(`${apiBaseUrl}/api/auth/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Login readiness check failed with ${response.status}`)
  }

  return response.json()
}

export async function getCurrentSession(sessionId: string, signal?: AbortSignal): Promise<AuthSessionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/auth/session`, {
    headers: { 'X-Legacy EHR-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Session readiness check failed with ${response.status}`)
  }

  return response.json()
}

export async function loginPatientPortal(
  input: PatientPortalLoginInput,
  signal?: AbortSignal,
): Promise<PatientPortalLoginResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal login check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalSession(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalSessionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/session`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal session check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalHome(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalHomeSummaryResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/home`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal home check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalProfile(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalProfileResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/profile`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal profile check failed with ${response.status}`)
  }

  return response.json()
}

export async function submitPatientPortalProfileChange(
  sessionId: string,
  input: PatientPortalProfileChangeInput,
  signal?: AbortSignal,
): Promise<PatientPortalProfileResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/profile/changes`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal profile change failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalAppointments(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalAppointmentsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/appointments`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal appointments check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalClinicalSummary(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalClinicalSummaryResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/clinical-summary`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal clinical summary check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalLabResults(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalLabResultsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/lab-results`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal lab results check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalMedicalReport(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMedicalReportResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/medical-report`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal medical report check failed with ${response.status}`)
  }

  return response.json()
}

export async function generatePatientPortalMedicalReport(
  sessionId: string,
  input: PatientPortalMedicalReportGenerationInput = {},
  signal?: AbortSignal,
): Promise<PatientPortalGeneratedMedicalReportResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/medical-report/generate`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal medical report generation failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalGeneratedMedicalReportAudit(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalGeneratedMedicalReportAuditResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/medical-report/audit`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal medical report audit check failed with ${response.status}`)
  }

  return response.json()
}

export async function downloadPatientPortalGeneratedMedicalReportPdf(
  sessionId: string,
  input: PatientPortalMedicalReportGenerationInput = {},
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/medical-report/pdf`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(errorText || `Patient portal medical report PDF download failed with ${response.status}`)
  }

  return response.blob()
}

export async function downloadPatientPortalGeneratedMedicalReportPackage(
  sessionId: string,
  input: PatientPortalMedicalReportGenerationInput = {},
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/medical-report/package`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(errorText || `Patient portal medical report package download failed with ${response.status}`)
  }

  return response.blob()
}

export async function getPatientPortalAppointmentRequestOptions(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalAppointmentRequestOptionsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/appointments/request-options`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal appointment request options failed with ${response.status}`)
  }

  return response.json()
}

export async function requestPatientPortalAppointment(
  sessionId: string,
  input: PatientPortalAppointmentRequestInput,
  signal?: AbortSignal,
): Promise<PatientPortalAppointmentRequestResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/appointments/requests`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal appointment request failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalMessages(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMessagesResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal messages check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalMessageRecipients(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMessageRecipientsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages/recipients`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal message recipients check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalMessageAudit(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMessageAuditResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages/audit`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal message audit check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalMessageComposeOptions(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMessageComposeOptionsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages/compose-options`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal message compose options check failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientPortalDocuments(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalDocumentsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/documents`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal documents check failed with ${response.status}`)
  }

  return response.json()
}

export async function downloadPatientPortalDocuments(
  sessionId: string,
  input: PatientPortalDocumentsDownloadInput,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/documents/download`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(errorText || `Patient portal document download failed with ${response.status}`)
  }

  return response.blob()
}

export async function getPatientPortalMessageThread(
  sessionId: string,
  messageId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMessageThreadResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages/${messageId}/thread`, {
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal message thread check failed with ${response.status}`)
  }

  return response.json()
}

export async function composePatientPortalMessage(
  sessionId: string,
  input: PatientPortalComposeMessageInput,
  signal?: AbortSignal,
): Promise<PatientPortalComposeMessageResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal message compose failed with ${response.status}`)
  }

  return response.json()
}

export async function requestPatientPortalPrescriptionRefill(
  sessionId: string,
  prescriptionId: string,
  input: PatientPortalPrescriptionRefillRequestInput,
  signal?: AbortSignal,
): Promise<PatientPortalComposeMessageResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/prescriptions/${encodeURIComponent(prescriptionId)}/refill-request`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-Legacy EHR-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify(input),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(`Patient portal prescription refill request failed with ${response.status}`)
  }

  return response.json()
}

export async function replyPatientPortalMessage(
  sessionId: string,
  messageId: string,
  input: PatientPortalReplyMessageInput,
  signal?: AbortSignal,
): Promise<PatientPortalReplyMessageResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages/${messageId}/reply`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal message reply failed with ${response.status}`)
  }

  return response.json()
}

export async function forwardPatientPortalMessage(
  sessionId: string,
  messageId: string,
  input: PatientPortalForwardMessageInput,
  signal?: AbortSignal,
): Promise<PatientPortalForwardMessageResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages/${messageId}/forward`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal message forward failed with ${response.status}`)
  }

  return response.json()
}

export async function readPatientPortalMessage(
  sessionId: string,
  messageId: string,
  signal?: AbortSignal,
): Promise<PatientPortalReadMessageResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages/${messageId}/read`, {
    method: 'PUT',
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal message read-status update failed with ${response.status}`)
  }

  return response.json()
}

export async function deletePatientPortalMessage(
  sessionId: string,
  messageId: string,
  signal?: AbortSignal,
): Promise<PatientPortalDeleteMessageResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages/${messageId}`, {
    method: 'DELETE',
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal message archive failed with ${response.status}`)
  }

  return response.json()
}

export async function archivePatientPortalMessages(
  sessionId: string,
  input: PatientPortalArchiveMessagesInput,
  signal?: AbortSignal,
): Promise<PatientPortalArchiveMessagesResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages/archive`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal selected message archive failed with ${response.status}`)
  }

  return response.json()
}

export async function endPatientPortalSession(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalSessionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/session`, {
    method: 'DELETE',
    headers: { 'X-Legacy EHR-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal session logout failed with ${response.status}`)
  }

  return response.json()
}

export async function logout(sessionId: string, signal?: AbortSignal): Promise<AuthSessionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/auth/logout`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ sessionId }),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Session logout failed with ${response.status}`)
  }

  return response.json()
}

export async function getLoginAudit(
  limit = 10,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AuthAuditResponse> {
  const headers: HeadersInit = {}
  if (sessionId) {
    headers['X-Legacy EHR-Session'] = sessionId
  }

  const response = await fetch(`${apiBaseUrl}/api/auth/login-audit?limit=${encodeURIComponent(String(limit))}`, {
    headers,
    signal,
  })
  if (!response.ok) {
    throw new Error(response.status === 401
      ? 'Login audit requires an active admin session.'
      : `Login audit load failed with ${response.status}`)
  }

  return response.json()
}

function sessionApiError(action: string, status: number, forbiddenRequirement?: string) {
  if (status === 401) {
    return `${action} requires an active Legacy EHR session.`
  }
  if (status === 403 && forbiddenRequirement) {
    return `${action} requires ${forbiddenRequirement}.`
  }
  return `${action} failed with ${status}`
}

function clinicalListApiError(action: string, status: number) {
  return sessionApiError(action, status, 'Medical/History access')
}

function appointmentApiError(action: string, status: number) {
  return sessionApiError(action, status, 'Appointment access')
}

function encounterApiError(action: string, status: number) {
  return sessionApiError(action, status, 'Encounter access')
}

function patientApiError(action: string, status: number) {
  return sessionApiError(action, status, 'Patient access')
}

function documentApiError(action: string, status: number) {
  return sessionApiError(action, status, 'Document access')
}

function messageApiError(action: string, status: number) {
  return sessionApiError(action, status, 'Message access')
}

function billingApiError(action: string, status: number) {
  return sessionApiError(action, status, 'Billing access')
}

function procedureApiError(action: string, status: number) {
  return sessionApiError(action, status, 'Procedure access')
}

export async function searchPatients(
  search: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientSearchResponse> {
  const params = new URLSearchParams()
  if (search.trim()) {
    params.set('search', search.trim())
  }
  params.set('limit', '25')

  const response = await fetch(`${apiBaseUrl}/api/patients?${params.toString()}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient search', response.status))
  }

  return response.json()
}

export async function getPatientChart(
  canonicalId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(canonicalId)}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient chart load', response.status))
  }

  return response.json()
}

export async function getPatientProviderAssignmentOptions(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientProviderAssignmentOptionsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patients/provider-options`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient provider options load', response.status))
  }

  return response.json()
}

export async function getPatientCareTeamOptions(
  patientId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientCareTeamOptionsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/care-team-options`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient care team options load', response.status))
  }

  return response.json()
}

export async function findPatientDuplicates(
  input: {
    firstName?: string | null
    lastName?: string | null
    dateOfBirth?: string | null
    phone?: string | null
    email?: string | null
    excludePatientId?: string | null
    limit?: number | null
  },
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDuplicateSearchResponse> {
  const params = new URLSearchParams()
  if (input.firstName?.trim()) {
    params.set('firstName', input.firstName.trim())
  }
  if (input.lastName?.trim()) {
    params.set('lastName', input.lastName.trim())
  }
  if (input.dateOfBirth?.trim()) {
    params.set('dateOfBirth', input.dateOfBirth.trim())
  }
  if (input.phone?.trim()) {
    params.set('phone', input.phone.trim())
  }
  if (input.email?.trim()) {
    params.set('email', input.email.trim())
  }
  if (input.excludePatientId?.trim()) {
    params.set('excludePatientId', input.excludePatientId.trim())
  }
  params.set('limit', String(input.limit ?? 10))

  const response = await fetch(`${apiBaseUrl}/api/patients/duplicates?${params.toString()}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient duplicate detection', response.status))
  }

  return response.json()
}

export async function getPatientMergePreview(
  targetPatientId: string,
  sourcePatientId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientMergePreviewResponse> {
  const params = new URLSearchParams({
    targetPatientId,
    sourcePatientId,
  })

  const response = await fetch(`${apiBaseUrl}/api/patients/merge-preview?${params.toString()}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient merge preview', response.status))
  }

  return response.json()
}

export async function createPatient(
  patient: PatientRegistrationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(patient),
    signal,
  })
  if (!response.ok) {
    if (response.status === 400) {
      const messages = await readValidationMessages(response)
      if (messages.length > 0) {
        throw new PatientRegistrationValidationError(messages)
      }
    }

    throw new Error(patientApiError('Patient registration', response.status))
  }

  return response.json()
}

async function readValidationMessages(response: Response) {
  try {
    const body = (await response.json()) as { errors?: Record<string, string[]>; title?: string }
    return Object.values(body.errors ?? {})
      .flat()
      .filter((message): message is string => typeof message === 'string' && message.trim().length > 0)
  } catch {
    return []
  }
}

export async function deletePatient(patientId: string, sessionId?: string | null, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok && response.status !== 404) {
    throw new Error(patientApiError('Patient delete', response.status))
  }
}

export async function updatePatientContact(
  patientId: string,
  contact: PatientContactUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/contact`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(contact),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient contact update', response.status))
  }

  return response.json()
}

export async function updatePatientDemographics(
  patientId: string,
  demographics: PatientDemographicsUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/demographics`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(demographics),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient demographics update', response.status))
  }

  return response.json()
}

export async function updatePatientDeceasedStatus(
  patientId: string,
  status: PatientDeceasedStatusUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/deceased-status`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(status),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient deceased status update', response.status))
  }

  return response.json()
}

export async function updatePatientPortalAccountReset(
  patientId: string,
  reset: PatientPortalAccountResetUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/portal-account/reset`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(reset),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient portal account reset update', response.status))
  }

  return response.json()
}

export async function updatePatientPortalAccountAccess(
  patientId: string,
  access: PatientPortalAccountAccessUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/portal-account/access`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(access),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient portal account access update', response.status))
  }

  return response.json()
}

export async function updatePatientGuardianContact(
  patientId: string,
  guardianContact: PatientGuardianContactUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/guardian-contact`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(guardianContact),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient guardian contact update', response.status))
  }

  return response.json()
}

export async function updatePatientEmployer(
  patientId: string,
  employer: PatientEmployerUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/employer`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(employer),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient employer update', response.status))
  }

  return response.json()
}

export async function updatePatientProviderAssignment(
  patientId: string,
  assignment: PatientProviderAssignmentUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/provider-assignment`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(assignment),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient provider assignment update', response.status))
  }

  return response.json()
}

export async function updatePatientCareTeam(
  patientId: string,
  careTeam: PatientCareTeamUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/care-team`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(careTeam),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient care team update', response.status))
  }

  return response.json()
}

export async function createPatientInsurance(
  patientId: string,
  insurance: PatientInsuranceMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/insurance`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(insurance),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient insurance create', response.status))
  }

  return response.json()
}

export async function updatePatientInsurance(
  insuranceId: string,
  insurance: PatientInsuranceMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/insurance/${encodeURIComponent(insuranceId)}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(insurance),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient insurance update', response.status))
  }

  return response.json()
}

export async function deletePatientInsurance(
  insuranceId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/insurance/${encodeURIComponent(insuranceId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(patientApiError('Patient insurance delete', response.status))
  }

  return response.json()
}

export async function searchAppointments(
  patientId: string,
  fromDate: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentSearchResponse> {
  const params = new URLSearchParams()
  if (patientId.trim()) {
    params.set('patientId', patientId.trim())
  }
  if (fromDate.trim()) {
    params.set('from', fromDate.trim())
  }
  params.set('limit', '25')

  const response = await fetch(`${apiBaseUrl}/api/appointments?${params.toString()}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment search', response.status))
  }

  return response.json()
}

export async function getAppointmentDetail(
  appointmentId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentDetail> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment detail load', response.status))
  }

  return response.json()
}

export async function getAppointmentFlowBoard(
  date?: string | null,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<FlowBoardResponse> {
  const params = new URLSearchParams()
  if (date?.trim()) {
    params.set('date', date.trim())
  }
  const suffix = params.size > 0 ? `?${params.toString()}` : ''
  const response = await fetch(`${apiBaseUrl}/api/appointments/flow-board${suffix}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Flow board load', response.status))
  }

  return response.json()
}

export async function getAppointmentWaitlist(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentWaitlistResponse> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/waitlist`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment waitlist load', response.status))
  }

  return response.json()
}

export async function createAppointment(
  appointment: AppointmentCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentDetail> {
  const response = await fetch(`${apiBaseUrl}/api/appointments`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(appointment),
    signal,
  })
  if (!response.ok) {
    if (response.status === 409) {
      const conflict = (await response.json().catch(() => null)) as {
        error?: string
        validation?: AppointmentAvailabilityValidationResponse
      } | null
      const validation = conflict?.validation
      const detail = validation?.messages?.length ? ` ${validation.messages.join(' ')}` : ''
      throw new Error(`${conflict?.error ?? 'Appointment conflicts with existing schedule availability.'}${detail}`)
    }
    throw new Error(appointmentApiError('Appointment create', response.status))
  }

  return response.json()
}

export async function validateAppointmentAvailability(
  appointment: AppointmentAvailabilityValidationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentAvailabilityValidationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/availability/validate`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(appointment),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment availability validation', response.status))
  }

  return response.json()
}

export async function dispatchAppointmentReminder(
  appointmentId: string,
  sessionId?: string | null,
  input?: AppointmentReminderDispatchInput | null,
  signal?: AbortSignal,
): Promise<AppointmentReminderDispatchResponse> {
  const hasDispatchInput = Boolean(input?.templateId?.trim())
  const response = await fetch(`${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}/reminders/dispatch`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, hasDispatchInput ? 'application/json' : undefined),
    body: hasDispatchInput ? JSON.stringify({ templateId: input?.templateId?.trim() }) : undefined,
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment reminder dispatch', response.status))
  }

  return response.json()
}

export async function getAppointmentReminderTemplates(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentReminderTemplateCatalogResponse> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/reminders/templates`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment reminder templates', response.status))
  }

  return response.json()
}

export async function retryAppointmentReminderDispatch(
  appointmentId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentReminderDispatchResponse> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}/reminders/dispatch/retry`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment reminder dispatch retry', response.status))
  }

  return response.json()
}

export async function getAppointmentReminderDispatchHistory(
  appointmentId?: string | null,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentReminderDispatchHistoryResponse> {
  const params = new URLSearchParams()
  if (appointmentId?.trim()) {
    params.set('appointmentId', appointmentId.trim())
  }
  const response = await fetch(`${apiBaseUrl}/api/appointments/reminders/dispatch-history?${params.toString()}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment reminder dispatch history', response.status))
  }

  return response.json()
}

export async function updateAppointmentStatus(
  appointmentId: string,
  update: AppointmentStatusUpdate,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentDetail> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}/status`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment status update', response.status))
  }

  return response.json()
}

export async function updateAppointment(
  appointmentId: string,
  update: AppointmentUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentDetail> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment update', response.status))
  }

  return response.json()
}

export async function rescheduleAppointmentOccurrence(
  appointmentId: string,
  occurrenceDate: string,
  update: AppointmentOccurrenceRescheduleInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentDetail> {
  const response = await fetch(
    `${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}/occurrences/${encodeURIComponent(occurrenceDate)}/reschedule`,
    {
      method: 'POST',
      headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
      body: JSON.stringify(update),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment occurrence reschedule', response.status))
  }

  return response.json()
}

export async function deleteAppointment(
  appointmentId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment delete', response.status))
  }
}

export async function restoreAppointmentOccurrence(
  appointmentId: string,
  occurrenceDate: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentDetail> {
  const response = await fetch(
    `${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}/recurrence-exceptions/${encodeURIComponent(occurrenceDate)}/restore`,
    {
      method: 'POST',
      headers: buildLegacyEhrSessionHeaders(sessionId),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(appointmentApiError('Appointment occurrence restore', response.status))
  }

  return response.json()
}

export async function searchEncounters(
  patientId: string,
  fromDate: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterSearchResponse> {
  const params = new URLSearchParams()
  if (patientId.trim()) {
    params.set('patientId', patientId.trim())
  }
  if (fromDate.trim()) {
    params.set('from', fromDate.trim())
  }
  params.set('limit', '25')

  const response = await fetch(`${apiBaseUrl}/api/encounters?${params.toString()}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter search', response.status))
  }

  return response.json()
}

export async function getEncounterDetail(
  encounter: number,
  sessionId?: string | null,
  signal?: AbortSignal,
  includeArchivedDocuments = false,
): Promise<EncounterDetail> {
  const query = includeArchivedDocuments ? '?includeArchivedDocuments=true' : ''
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}${query}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter detail load', response.status))
  }

  return response.json()
}

export async function getEncounterSoapNoteTemplates(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterSoapNoteTemplateCatalogResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/soap-note-templates`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter SOAP note templates', response.status))
  }

  return response.json()
}

export async function createEncounter(
  encounter: EncounterCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDetail> {
  const response = await fetch(`${apiBaseUrl}/api/encounters`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(encounter),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter create', response.status))
  }

  return response.json()
}

export async function updateEncounter(
  encounter: number,
  update: EncounterUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDetail> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter update', response.status))
  }

  return response.json()
}

export async function deleteEncounter(
  encounter: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter delete', response.status))
  }
}

export async function createEncounterVitals(
  encounter: number,
  vitals: EncounterVitalsCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterFormMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/vitals`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(vitals),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter vitals create', response.status))
  }

  return response.json()
}

export async function createEncounterSoapNote(
  encounter: number,
  soapNote: EncounterSoapNoteCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterFormMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/soap-notes`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(soapNote),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter SOAP note create', response.status))
  }

  return response.json()
}

export async function signEncounter(
  encounter: number,
  signature: EncounterSignInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterSignatureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/sign`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(signature),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter sign-off', response.status))
  }

  return response.json()
}

export async function createEncounterDocument(
  encounter: number,
  document: EncounterDocumentCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter document attach', response.status))
  }

  return response.json()
}

export async function createEncounterBinaryDocument(
  encounter: number,
  document: EncounterBinaryDocumentCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/binary`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Binary encounter document attach', response.status))
  }

  return response.json()
}

export async function createEncounterExternalLinkDocument(
  encounter: number,
  document: EncounterExternalLinkDocumentCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/external-link`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('External-link encounter document attach', response.status))
  }

  return response.json()
}

export async function updateEncounterDocumentMetadata(
  encounter: number,
  documentId: number,
  document: PatientDocumentMetadataUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/${documentId}/metadata`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter document metadata update', response.status))
  }

  return response.json()
}

export async function moveEncounterDocument(
  encounter: number,
  documentId: number,
  input: EncounterDocumentMoveInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMoveResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/${documentId}/move`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter document move', response.status))
  }

  return response.json()
}

export async function replaceEncounterDocumentContent(
  encounter: number,
  documentId: number,
  document: PatientDocumentContentReplaceInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/${documentId}/content`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter document content replacement', response.status))
  }

  return response.json()
}

export async function replaceEncounterDocumentBinaryContent(
  encounter: number,
  documentId: number,
  document: PatientDocumentBinaryContentReplaceInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/${documentId}/content/binary`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter binary document content replacement', response.status))
  }

  return response.json()
}

export async function softDeleteEncounterDocument(
  encounter: number,
  documentId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/${documentId}/soft-delete`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter document archive', response.status))
  }

  return response.json()
}

export async function restoreEncounterDocument(
  encounter: number,
  documentId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/${documentId}/restore`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter document restore', response.status))
  }

  return response.json()
}

export async function signEncounterDocument(
  encounter: number,
  documentId: number,
  signature: PatientDocumentSignInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/${documentId}/sign`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(signature),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter document sign-off', response.status))
  }

  return response.json()
}

export async function denyEncounterDocument(
  encounter: number,
  documentId: number,
  signature: PatientDocumentSignInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/documents/${documentId}/sign`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(signature),
    signal,
  })
  if (!response.ok) {
    throw new Error(encounterApiError('Encounter document denial', response.status))
  }

  return response.json()
}

export async function deleteEncounterSignature(
  encounter: number,
  signatureId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/signatures/${signatureId}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok && response.status !== 404) {
    throw new Error(encounterApiError('Encounter signature delete', response.status))
  }
}

export async function getClinicalLists(
  patientId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/${encodeURIComponent(patientId.trim())}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical lists load', response.status))
  }

  return response.json()
}

export async function createClinicalAllergy(
  allergy: ClinicalAllergyCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/allergies`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(allergy),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical allergy create', response.status))
  }

  return response.json()
}

export async function createClinicalProblem(
  problem: ClinicalProblemCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/problems`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(problem),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical problem create', response.status))
  }

  return response.json()
}

export async function deactivateClinicalProblem(
  problemId: string,
  update: ClinicalListDeactivateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/problems/${encodeURIComponent(problemId)}/deactivate`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical problem deactivate', response.status))
  }

  return response.json()
}

export async function deleteClinicalProblem(
  problemId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/problems/${encodeURIComponent(problemId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical problem delete', response.status))
  }
}

export async function createClinicalMedication(
  medication: ClinicalMedicationCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/medications`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(medication),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical medication create', response.status))
  }

  return response.json()
}

export async function deactivateClinicalMedication(
  medicationId: string,
  update: ClinicalListDeactivateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/medications/${encodeURIComponent(medicationId)}/deactivate`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical medication deactivate', response.status))
  }

  return response.json()
}

export async function deleteClinicalMedication(
  medicationId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/medications/${encodeURIComponent(medicationId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical medication delete', response.status))
  }
}

export async function deactivateClinicalAllergy(
  allergyId: string,
  update: ClinicalListDeactivateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/allergies/${encodeURIComponent(allergyId)}/deactivate`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical allergy deactivate', response.status))
  }

  return response.json()
}

export async function deleteClinicalAllergy(
  allergyId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/allergies/${encodeURIComponent(allergyId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical allergy delete', response.status))
  }
}

export async function searchClinicalMedicationVocabulary(
  query: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<MedicationVocabularyItem[]> {
  const params = new URLSearchParams()
  if (query.trim()) {
    params.set('query', query.trim())
  }
  const url = `${apiBaseUrl}/api/clinical-lists/medication-vocabulary${params.size ? `?${params.toString()}` : ''}`
  const response = await fetch(url, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical medication vocabulary search', response.status))
  }

  return response.json()
}

export async function createClinicalPrescription(
  prescription: ClinicalPrescriptionCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/prescriptions`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(prescription),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical prescription create', response.status))
  }

  return response.json()
}

export async function deactivateClinicalPrescription(
  prescriptionId: string,
  update: ClinicalPrescriptionDeactivateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}/deactivate`,
    {
      method: 'PUT',
      headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
      body: JSON.stringify(update),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical prescription deactivate', response.status))
  }

  return response.json()
}

export async function refillClinicalPrescription(
  prescriptionId: string,
  refill: ClinicalPrescriptionRefillInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}/refill`,
    {
      method: 'PUT',
      headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
      body: JSON.stringify(refill),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical prescription refill', response.status))
  }

  return response.json()
}

export async function routeClinicalPrescriptionToPharmacy(
  prescriptionId: string,
  route: ClinicalPrescriptionPharmacyRouteInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalPrescriptionPharmacyRouteResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}/route-pharmacy`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(route),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical prescription pharmacy route', response.status))
  }

  return response.json()
}

export async function getClinicalPrescriptionAuditHistory(
  prescriptionId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalPrescriptionAuditHistoryResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}/audit-history`,
    {
      headers: buildLegacyEhrSessionHeaders(sessionId),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical prescription audit history', response.status))
  }

  return response.json()
}

export async function approveClinicalPrescriptionRefillRequest(
  messageId: number,
  approval: ClinicalPrescriptionRefillApprovalInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/clinical-lists/prescription-refill-requests/${encodeURIComponent(String(messageId))}/approve`,
    {
      method: 'PUT',
      headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
      body: JSON.stringify(approval),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical prescription refill request approval', response.status))
  }

  return response.json()
}

export async function deleteClinicalPrescription(
  prescriptionId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical prescription delete', response.status))
  }
}

export async function createClinicalImmunization(
  immunization: ClinicalImmunizationCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/immunizations`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(immunization),
    signal,
  })
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical immunization create', response.status))
  }

  return response.json()
}

export async function markClinicalImmunizationEnteredInError(
  immunizationId: number,
  update: ClinicalImmunizationErrorInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/clinical-lists/immunizations/${encodeURIComponent(String(immunizationId))}/entered-in-error`,
    {
      method: 'PUT',
      headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
      body: JSON.stringify(update),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical immunization entered-in-error update', response.status))
  }

  return response.json()
}

export async function deleteClinicalImmunization(
  immunizationId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(
    `${apiBaseUrl}/api/clinical-lists/immunizations/${encodeURIComponent(String(immunizationId))}`,
    {
      method: 'DELETE',
      headers: buildLegacyEhrSessionHeaders(sessionId),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(clinicalListApiError('Clinical immunization delete', response.status))
  }
}

export async function getPatientMessages(
  patientId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientMessagesResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(patientId.trim())}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(messageApiError('Patient messages load', response.status))
  }

  return response.json()
}

export async function getPatientDocuments(
  patientId: string,
  includeArchived = false,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentsResponse> {
  const query = includeArchived ? '?includeArchived=true' : ''
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(patientId.trim())}${query}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient documents load', response.status))
  }

  return response.json()
}

export async function getPatientDocumentOcrQueue(
  patientId?: string | null,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentOcrQueueResponse> {
  const query = patientId && patientId.trim().length > 0
    ? `?patientId=${encodeURIComponent(patientId.trim())}`
    : ''
  const response = await fetch(`${apiBaseUrl}/api/documents/ocr-queue${query}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Document OCR queue load', response.status))
  }

  return response.json()
}

export async function getPatientDocumentRoutingQueue(
  patientId?: string | null,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentRoutingQueueResponse> {
  const query = patientId && patientId.trim().length > 0
    ? `?patientId=${encodeURIComponent(patientId.trim())}`
    : ''
  const response = await fetch(`${apiBaseUrl}/api/documents/routing-queue${query}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Document routing queue load', response.status))
  }

  return response.json()
}

export async function getPatientDocumentRetentionPolicy(
  patientId?: string | null,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentRetentionPolicyResponse> {
  const query = patientId && patientId.trim().length > 0
    ? `?patientId=${encodeURIComponent(patientId.trim())}`
    : ''
  const response = await fetch(`${apiBaseUrl}/api/documents/retention-policy${query}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Document retention policy load', response.status))
  }

  return response.json()
}

export async function completePatientDocumentOcr(
  documentId: number,
  input: PatientDocumentOcrCompleteInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentOcrCompleteResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/ocr/complete`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document OCR complete', response.status))
  }

  return response.json()
}

export async function disposePatientDocumentRetention(
  documentId: number,
  input: PatientDocumentRetentionDispositionInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentRetentionDispositionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/retention/dispose`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document retention disposition', response.status))
  }

  return response.json()
}

export async function getPatientDocumentContent(
  documentId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentContentResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/content`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document content load', response.status))
  }

  return response.json()
}

export async function downloadPatientDocument(
  documentId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/download`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document download', response.status))
  }

  return response.blob()
}

export async function createPatientDocument(
  document: PatientDocumentCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document create', response.status))
  }

  return response.json()
}

export async function createPatientBinaryDocument(
  document: PatientDocumentBinaryCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/binary`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Binary patient document create', response.status))
  }

  return response.json()
}

export async function createPatientScannerCapture(
  document: PatientDocumentScannerCaptureInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/scanner-captures`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Scanner-captured patient document create', response.status))
  }

  return response.json()
}

export async function createPatientExternalLinkDocument(
  document: PatientDocumentExternalLinkCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/external-link`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('External-link patient document create', response.status))
  }

  return response.json()
}

export async function updatePatientDocumentMetadata(
  documentId: number,
  document: PatientDocumentMetadataUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/metadata`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document metadata update', response.status))
  }

  return response.json()
}

export async function replacePatientDocumentContent(
  documentId: number,
  document: PatientDocumentContentReplaceInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/content`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document content replacement', response.status))
  }

  return response.json()
}

export async function replacePatientDocumentBinaryContent(
  documentId: number,
  document: PatientDocumentBinaryContentReplaceInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/content/binary`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Binary patient document content replacement', response.status))
  }

  return response.json()
}

export async function softDeletePatientDocument(
  documentId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/soft-delete`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document archive', response.status))
  }

  return response.json()
}

export async function restorePatientDocument(
  documentId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/restore`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document restore', response.status))
  }

  return response.json()
}

export async function signPatientDocument(
  documentId: number,
  signature: PatientDocumentSignInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/sign`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(signature),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document sign-off', response.status))
  }

  return response.json()
}

export async function deletePatientDocument(
  documentId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(documentApiError('Patient document delete', response.status))
  }
}

export async function createPatientMessage(
  message: PatientMessageCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientMessageMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(message),
    signal,
  })
  if (!response.ok) {
    throw new Error(messageApiError('Patient message create', response.status))
  }

  return response.json()
}

export async function updatePatientMessageStatus(
  messageId: string,
  update: PatientMessageStatusUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientMessageMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(messageId)}/status`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(messageApiError('Patient message update', response.status))
  }

  return response.json()
}

export async function updatePatientMessageContent(
  messageId: string,
  update: PatientMessageContentUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientMessageMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(messageId)}/content`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(messageApiError('Patient message content update', response.status))
  }

  return response.json()
}

export async function updatePatientMessageAssignment(
  messageId: string,
  update: PatientMessageAssignmentUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientMessageMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(messageId)}/assignment`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(messageApiError('Patient message assignment update', response.status))
  }

  return response.json()
}

export async function replyToPatientMessage(
  messageId: string,
  reply: PatientMessageReplyInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientMessageMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(messageId)}/reply`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(reply),
    signal,
  })
  if (!response.ok) {
    throw new Error(messageApiError('Patient message reply', response.status))
  }

  return response.json()
}

export async function softDeletePatientMessage(
  messageId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientMessageMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(messageId)}/soft-delete`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(messageApiError('Patient message archive', response.status))
  }

  return response.json()
}

export async function deletePatientMessage(
  messageId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(messageId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(messageApiError('Patient message delete', response.status))
  }
}

export async function getProcedureResults(
  patientId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureResultsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/${encodeURIComponent(patientId.trim())}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure results load', response.status))
  }

  return response.json()
}

export async function getProcedureReportReviewQueue(
  status = 'unreviewed',
  filters: ProcedureReportReviewQueueFilters = {},
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureReportReviewQueueResponse> {
  const params = new URLSearchParams({ status })
  if (filters.patientId?.trim()) {
    params.set('patientId', filters.patientId.trim())
  }
  if (filters.providerId !== undefined && filters.providerId !== null && String(filters.providerId).trim()) {
    params.set('providerId', String(filters.providerId).trim())
  }
  if (filters.labId !== undefined && filters.labId !== null && String(filters.labId).trim()) {
    params.set('labId', String(filters.labId).trim())
  }
  if (filters.fromDate?.trim()) {
    params.set('fromDate', filters.fromDate.trim())
  }
  if (filters.toDate?.trim()) {
    params.set('toDate', filters.toDate.trim())
  }
  if (filters.limit) {
    params.set('limit', String(filters.limit))
  }
  const response = await fetch(`${apiBaseUrl}/api/procedures/report-review-queue?${params}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure report review queue load', response.status))
  }

  return response.json()
}

export async function getProcedureOrderQueue(
  status = 'ready-to-send',
  filters: ProcedureReportReviewQueueFilters = {},
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureOrderQueueResponse> {
  const params = new URLSearchParams({ status })
  if (filters.patientId?.trim()) {
    params.set('patientId', filters.patientId.trim())
  }
  if (filters.providerId !== undefined && filters.providerId !== null && String(filters.providerId).trim()) {
    params.set('providerId', String(filters.providerId).trim())
  }
  if (filters.labId !== undefined && filters.labId !== null && String(filters.labId).trim()) {
    params.set('labId', String(filters.labId).trim())
  }
  if (filters.fromDate?.trim()) {
    params.set('fromDate', filters.fromDate.trim())
  }
  if (filters.toDate?.trim()) {
    params.set('toDate', filters.toDate.trim())
  }
  if (filters.limit) {
    params.set('limit', String(filters.limit))
  }
  const response = await fetch(`${apiBaseUrl}/api/procedures/order-queue?${params}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order queue load', response.status))
  }

  return response.json()
}

export async function getProcedureLabProviders(
  includeInactive = false,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureLabProviderDirectoryResponse> {
  const params = new URLSearchParams()
  if (includeInactive) {
    params.set('includeInactive', 'true')
  }

  const query = params.toString()
  const suffix = query ? `?${query}` : ''
  const response = await fetch(`${apiBaseUrl}/api/procedures/lab-providers${suffix}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure lab provider directory load', response.status))
  }

  return response.json()
}

export async function getProcedureLabProviderAddressBook(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureLabProviderAddressBookResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/lab-provider-address-book`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure lab provider address book load', response.status))
  }

  return response.json()
}

export async function getProcedureOrderCatalog(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureOrderCatalogResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/order-catalog`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order catalog load', response.status))
  }

  return response.json()
}

export async function createProcedureOrderCatalogItem(
  input: ProcedureOrderCatalogMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureOrderCatalogMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/order-catalog`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order catalog create', response.status))
  }

  return response.json()
}

export async function updateProcedureOrderCatalogItem(
  itemId: number,
  input: ProcedureOrderCatalogMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureOrderCatalogMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/order-catalog/${itemId}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order catalog update', response.status))
  }

  return response.json()
}

export async function deleteProcedureOrderCatalogItem(
  itemId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/order-catalog/${itemId}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order catalog delete', response.status))
  }
}

export async function importProcedureOrderCatalogCompendium(
  input: ProcedureOrderCatalogImportInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureOrderCatalogImportResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/order-catalog/import-compendium`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order catalog compendium import', response.status))
  }

  return response.json()
}

export async function createProcedureLabProviderAddressBookOrganization(
  input: ProcedureLabProviderAddressBookMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureLabProviderAddressBookMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/lab-provider-address-book`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure lab provider address book create', response.status))
  }

  return response.json()
}

export async function deleteProcedureLabProviderAddressBookOrganization(
  organizationId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/lab-provider-address-book/${organizationId}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure lab provider address book delete', response.status))
  }
}

export async function createProcedureLabProvider(
  input: ProcedureLabProviderMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureLabProviderMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/lab-providers`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure lab provider create', response.status))
  }

  return response.json()
}

export async function updateProcedureLabProvider(
  providerId: number,
  input: ProcedureLabProviderMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureLabProviderMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/lab-providers/${providerId}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure lab provider update', response.status))
  }

  return response.json()
}

export async function deleteProcedureLabProvider(
  providerId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/lab-providers/${providerId}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure lab provider delete', response.status))
  }
}

export async function createProcedureOrder(
  input: ProcedureOrderCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/orders`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order create', response.status))
  }

  return response.json()
}

export async function updateProcedureOrderStatus(
  orderId: number,
  input: ProcedureOrderStatusUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/orders/${encodeURIComponent(String(orderId))}/status`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order status update', response.status))
  }

  return response.json()
}

export async function transmitProcedureOrder(
  orderId: number,
  input: ProcedureOrderTransmitInput = {},
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/orders/${encodeURIComponent(String(orderId))}/transmit`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order transmit', response.status))
  }

  return response.json()
}

export async function updateProcedureOrder(
  orderId: number,
  input: ProcedureOrderUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/orders/${encodeURIComponent(String(orderId))}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order update', response.status))
  }

  return response.json()
}

export async function createProcedureReport(
  input: ProcedureReportCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/reports`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure report create', response.status))
  }

  return response.json()
}

export async function updateProcedureReport(
  reportId: number,
  input: ProcedureReportUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/reports/${encodeURIComponent(String(reportId))}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure report update', response.status))
  }

  return response.json()
}

export async function signProcedureReport(
  reportId: number,
  input: ProcedureReportSignInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/reports/${encodeURIComponent(String(reportId))}/sign`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure report sign-off', response.status))
  }

  return response.json()
}

export async function assignProcedureReportReviewer(
  reportId: number,
  input: ProcedureReportReviewAssignmentInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/reports/${encodeURIComponent(String(reportId))}/review-assignment`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure report reviewer assignment', response.status))
  }

  return response.json()
}

export async function reopenProcedureReportReview(
  reportId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/reports/${encodeURIComponent(String(reportId))}/reopen-review`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure report review reopen', response.status))
  }

  return response.json()
}

export async function bulkSignProcedureReports(
  input: ProcedureReportBulkSignInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureReportBulkSignResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/reports/bulk-sign`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure report bulk sign-off', response.status))
  }

  return response.json()
}

export async function createProcedureSpecimen(
  input: ProcedureSpecimenCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/specimens`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure specimen create', response.status))
  }

  return response.json()
}

export async function createProcedureResult(
  input: ProcedureResultCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/results`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure result create', response.status))
  }

  return response.json()
}

export async function updateProcedureResult(
  resultId: number,
  input: ProcedureResultUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/results/${encodeURIComponent(String(resultId))}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure result update', response.status))
  }

  return response.json()
}

export async function deleteProcedureOrder(
  orderId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/orders/${encodeURIComponent(String(orderId))}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(procedureApiError('Procedure order delete', response.status))
  }
}

export async function getPatientBilling(
  patientId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<PatientBillingResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/${encodeURIComponent(patientId.trim())}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Patient billing load', response.status))
  }

  return response.json()
}

export async function getStatementBatch(
  limit = 10,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<StatementBatchResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/statements/batch?limit=${encodeURIComponent(String(limit))}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Statement batch load', response.status))
  }

  return response.json()
}

export async function getCollectionsWorkQueue(
  limit = 10,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<CollectionsWorkQueueResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/collections/work-queue?limit=${encodeURIComponent(String(limit))}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Collections work queue load', response.status))
  }

  return response.json()
}

export async function createCollectionsFollowUp(
  input: CollectionsFollowUpCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<CollectionsFollowUpMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/collections/follow-ups`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Collections follow-up create', response.status))
  }

  return response.json()
}

export async function downloadStatementBatchPackage(
  limit = 10,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${apiBaseUrl}/api/billing/statements/batch/package.zip?limit=${encodeURIComponent(String(limit))}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Statement batch package download', response.status))
  }

  return response.blob()
}

export async function prepareStatementBatchDeliveryManifest(
  limit = 10,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<StatementBatchDeliveryResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/statements/batch/delivery-manifest?limit=${encodeURIComponent(String(limit))}`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Statement batch delivery manifest prepare', response.status))
  }

  return response.json()
}

export async function dispatchStatementBatchDelivery(
  limit = 10,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<StatementBatchDispatchResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/statements/batch/dispatch?limit=${encodeURIComponent(String(limit))}`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Statement batch dispatch handoff', response.status))
  }

  return response.json()
}

export async function getStatementDispatchHistory(
  limit = 10,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<StatementDeliveryAuditHistoryResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/statements/batch/dispatch-history?limit=${encodeURIComponent(String(limit))}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Statement dispatch history load', response.status))
  }

  return response.json()
}

export async function deliverStatementBatchToPortal(
  limit = 10,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<StatementPortalDeliveryResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/statements/batch/portal-delivery?limit=${encodeURIComponent(String(limit))}`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Statement portal delivery', response.status))
  }

  return response.json()
}

export async function queueStatementBatchEmailOutbox(
  limit = 10,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<StatementEmailOutboxResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/statements/batch/email-outbox?limit=${encodeURIComponent(String(limit))}`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Statement email outbox queue', response.status))
  }

  return response.json()
}

export async function downloadBillingStatementPdf(
  patientId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${apiBaseUrl}/api/billing/${encodeURIComponent(patientId.trim())}/statement.pdf`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing statement PDF download', response.status))
  }

  return response.blob()
}

export async function downloadBillingPaymentReceiptPdf(
  activityId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${apiBaseUrl}/api/billing/payments/${encodeURIComponent(activityId)}/receipt.pdf`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing payment receipt PDF download', response.status))
  }

  return response.blob()
}

export async function createBillingLine(
  input: BillingLineCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingLineMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/lines`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing line create', response.status))
  }

  return response.json()
}

export async function getBillingChargeTemplate(
  templateId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingChargeTemplate> {
  const response = await fetch(`${apiBaseUrl}/api/billing/charge-templates/${encodeURIComponent(templateId)}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing charge template lookup', response.status))
  }

  return response.json()
}

export async function updateBillingLineStatus(
  billingLineId: string,
  input: BillingLineStatusUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingLineMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/lines/${encodeURIComponent(billingLineId)}/status`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing line status update', response.status))
  }

  return response.json()
}

export async function updateBillingLine(
  billingLineId: string,
  input: BillingLineUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingLineMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/lines/${encodeURIComponent(billingLineId)}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing line update', response.status))
  }

  return response.json()
}

export async function deleteBillingLine(
  billingLineId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/billing/lines/${encodeURIComponent(billingLineId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing line delete', response.status))
  }
}

export async function createBillingClaimStatus(
  input: BillingClaimCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingClaimMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/claims`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing claim status create', response.status))
  }

  return response.json()
}

export async function updateBillingClaimStatus(
  claimId: string,
  input: BillingClaimStatusUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingClaimMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/claims/${encodeURIComponent(claimId)}/status`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing claim status update', response.status))
  }

  return response.json()
}

export async function scrubBillingClaimStatus(
  claimId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingClaimMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/claims/${encodeURIComponent(claimId)}/scrub`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing claim scrub', response.status))
  }

  return response.json()
}

export async function generateBillingClaimStatus(
  claimId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingClaimMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/claims/${encodeURIComponent(claimId)}/generate`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing claim generation', response.status))
  }

  return response.json()
}

export async function resubmitBillingClaimStatus(
  claimId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingClaimMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/claims/${encodeURIComponent(claimId)}/resubmit`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing claim resubmission', response.status))
  }

  return response.json()
}

export async function denyBillingClaimStatus(
  claimId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingClaimMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/claims/${encodeURIComponent(claimId)}/deny`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing claim denial', response.status))
  }

  return response.json()
}

export async function clearBillingClaimStatus(
  claimId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingClaimMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/claims/${encodeURIComponent(claimId)}/clear`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing claim clearing', response.status))
  }

  return response.json()
}

export async function adjudicateBillingClaimStatus(
  claimId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingPaymentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/claims/${encodeURIComponent(claimId)}/adjudicate`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing claim adjudication', response.status))
  }

  return response.json()
}

export async function deleteBillingClaimStatus(
  claimId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/billing/claims/${encodeURIComponent(claimId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing claim status delete', response.status))
  }
}

export async function createBillingPatientPayment(
  input: BillingPatientPaymentCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingPaymentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/payments/patient-payments`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing patient payment create', response.status))
  }

  return response.json()
}

export async function createBillingPatientRefund(
  input: BillingPatientRefundCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingPaymentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/payments/patient-refunds`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing patient refund create', response.status))
  }

  return response.json()
}

export async function createBillingInsurancePayment(
  input: BillingInsurancePaymentCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingPaymentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/payments/insurance-payments`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing insurance payment create', response.status))
  }

  return response.json()
}

export async function createBillingInsuranceReversal(
  input: BillingInsuranceReversalCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingPaymentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/payments/insurance-reversals`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing insurance reversal create', response.status))
  }

  return response.json()
}

export async function createBillingAdjustmentReversal(
  input: BillingAdjustmentReversalCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingPaymentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/payments/adjustment-reversals`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing adjustment reversal create', response.status))
  }

  return response.json()
}

export async function importBillingEobBatch(
  input: BillingEobBatchImportInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingEobBatchImportResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/eob-batches/import`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing EOB batch import', response.status))
  }

  return response.json()
}

export async function voidBillingPaymentPosting(
  activityId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingPaymentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/payments/${encodeURIComponent(activityId)}/void`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing payment posting void', response.status))
  }

  return response.json()
}

export async function deleteBillingPaymentPosting(
  activityId: string,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/billing/payments/${encodeURIComponent(activityId)}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing payment posting delete', response.status))
  }
}

export async function getAdministrationDirectory(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationDirectoryResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/directory`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Administration directory load', response.status))
  }

  return response.json()
}

export async function acceptAdministrationPortalProfileReview(
  requestId: string | number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationPortalProfileReviewMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/administration/portal-activity/profile-reviews/${encodeURIComponent(String(requestId))}/accept`,
    {
      method: 'PUT',
      headers: buildLegacyEhrSessionHeaders(sessionId),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(adminApiError('Portal profile review accept', response.status))
  }

  return response.json()
}

export async function revertAdministrationPortalProfileReview(
  requestId: string | number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationPortalProfileReviewMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/administration/portal-activity/profile-reviews/${encodeURIComponent(String(requestId))}/revert`,
    {
      method: 'PUT',
      headers: buildLegacyEhrSessionHeaders(sessionId),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(adminApiError('Portal profile review revert', response.status))
  }

  return response.json()
}

export async function createAdministrationUser(
  input: AdministrationUserMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationUserMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/users`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Administration user create', response.status))
  }

  return response.json()
}

export async function updateAdministrationUser(
  userId: number,
  input: AdministrationUserMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationUserMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/users/${userId}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Administration user update', response.status))
  }

  return response.json()
}

export async function deleteAdministrationUser(
  userId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/administration/users/${userId}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Administration user delete', response.status))
  }
}

export async function createAdministrationFacility(
  input: AdministrationFacilityMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationFacilityMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/facilities`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Administration facility create', response.status))
  }

  return response.json()
}

export async function updateAdministrationFacility(
  facilityId: number,
  input: AdministrationFacilityMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationFacilityMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/facilities/${facilityId}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Administration facility update', response.status))
  }

  return response.json()
}

export async function deleteAdministrationFacility(
  facilityId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/administration/facilities/${facilityId}`, {
    method: 'DELETE',
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Administration facility delete', response.status))
  }
}

export async function grantAdministrationAccessPermission(
  input: AdministrationAccessPermissionMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationAccessPermissionMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/access-control/group-permissions`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Administration access permission grant', response.status))
  }

  return response.json()
}

export async function revokeAdministrationAccessPermission(
  input: Pick<AdministrationAccessPermissionMutationInput, 'groupValue' | 'sectionValue' | 'permissionValue'>,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationAccessPermissionMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/administration/access-control/group-permissions/${encodeURIComponent(input.groupValue)}/${encodeURIComponent(input.sectionValue)}/${encodeURIComponent(input.permissionValue)}`,
    {
      method: 'DELETE',
      headers: buildLegacyEhrSessionHeaders(sessionId),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(adminApiError('Administration access permission revoke', response.status))
  }

  return response.json()
}

export async function grantAdministrationAccessUserMembership(
  input: AdministrationAccessUserMembershipMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationAccessUserMembershipMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/access-control/user-memberships`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Administration access user membership grant', response.status))
  }

  return response.json()
}

export async function revokeAdministrationAccessUserMembership(
  input: AdministrationAccessUserMembershipMutationInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<AdministrationAccessUserMembershipMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/administration/access-control/user-memberships/${encodeURIComponent(input.userValue)}/${encodeURIComponent(input.groupValue)}`,
    {
      method: 'DELETE',
      headers: buildLegacyEhrSessionHeaders(sessionId),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(adminApiError('Administration access user membership revoke', response.status))
  }

  return response.json()
}

export async function getOperationalReports(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<OperationalReportsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/reports/operational`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(
      adminApiError('Operational reports load', response.status, 'Patient Report access', 'an active Legacy EHR session'),
    )
  }

  return response.json()
}

export async function getInventory(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory', response.status))
  }

  return response.json()
}

export async function createInventoryTransaction(
  input: InventoryTransactionCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryMutationResponse> {
  const endpoint = input.transactionType === 'return' ? 'returns' : 'transactions'
  const response = await fetch(`${apiBaseUrl}/api/inventory/${endpoint}`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory transaction', response.status))
  }

  return response.json()
}

export async function updateInventoryLotMetadata(
  lotId: number,
  input: InventoryLotMetadataUpdateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryLotMetadataUpdateResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/lots/${lotId}`, {
    method: 'PUT',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory lot update', response.status))
  }

  return response.json()
}

export async function getInventoryLotMetadataHistory(
  lotId: number,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryLotMetadataAuditItem[]> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/lots/${lotId}/metadata-history`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory lot history', response.status))
  }

  return response.json()
}

export async function destroyInventoryLot(
  lotId: number,
  input: InventoryLotDestructionInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryLotDestructionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/lots/${lotId}/destructions`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory lot destruction', response.status))
  }

  return response.json()
}

export async function createInventoryExpiryDisposition(lotId: number, input: InventoryExpiryDispositionInput, sessionId?: string | null): Promise<InventoryExpiryDispositionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/lots/${lotId}/expiry-dispositions`, { method: 'POST', headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'), body: JSON.stringify(input) })
  if (!response.ok) throw new Error(adminApiError('Inventory expiry disposition', response.status))
  return response.json()
}

export async function createInventoryPatientSale(
  input: InventoryPatientSaleCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryPatientSaleResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/patient-sales`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory patient sale', response.status))
  }
  return response.json()
}

export async function allocateInventoryPatientSale(input: InventoryPatientSaleAllocationCreateInput, sessionId?: string | null): Promise<InventoryPatientSaleAllocationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/patient-sales/allocate`, { method: 'POST', headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'), body: JSON.stringify(input) })
  if (!response.ok) throw new Error(adminApiError('Inventory sale allocation', response.status))
  return response.json()
}

export async function getInventoryMedicationCatalog(sessionId?: string | null, signal?: AbortSignal): Promise<InventoryMedicationCatalogItem[]> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/medication-catalog`, { headers: buildLegacyEhrSessionHeaders(sessionId), signal })
  if (!response.ok) throw new Error(adminApiError('Inventory medication catalog', response.status))
  return response.json()
}

export async function updateInventoryMedicationLink(itemId: number, input: InventoryMedicationLinkUpdateInput, sessionId?: string | null): Promise<InventoryMedicationLink> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/items/${itemId}/medication-link`, { method: 'PUT', headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'), body: JSON.stringify(input) })
  if (!response.ok) throw new Error(adminApiError('Inventory medication link', response.status))
  return response.json()
}

export async function dispenseInventoryPrescription(input: InventoryPrescriptionDispenseInput, sessionId?: string | null): Promise<InventoryPrescriptionDispenseResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/prescription-dispensations`, { method: 'POST', headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'), body: JSON.stringify(input) })
  if (!response.ok) throw new Error(adminApiError('Prescription dispensing', response.status))
  return response.json()
}

export async function createInventoryTransfer(
  input: InventoryTransferCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/transfers`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory transfer', response.status))
  }

  return response.json()
}

export async function getInventoryVendors(
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryVendorListResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/vendors`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory vendors', response.status))
  }
  return response.json()
}

export async function createInventoryVendor(
  input: InventoryVendorCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryVendor> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/vendors`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory vendor', response.status))
  }
  return response.json()
}

export async function createInventoryPurchaseReceipt(
  input: InventoryPurchaseReceiptCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryPurchaseReceiptResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/purchase-receipts`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory purchase receipt', response.status))
  }
  return response.json()
}

export async function getInventoryPurchaseRequisitions(sessionId?: string | null, signal?: AbortSignal): Promise<InventoryPurchaseRequisition[]> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/purchase-requisitions`, { headers: buildLegacyEhrSessionHeaders(sessionId), signal })
  if (!response.ok) throw new Error(adminApiError('Inventory purchase requisitions', response.status))
  return response.json()
}

export async function createInventoryPurchaseRequisition(input: InventoryPurchaseRequisitionCreateInput, sessionId?: string | null): Promise<InventoryPurchaseRequisition> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/purchase-requisitions`, { method: 'POST', headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'), body: JSON.stringify(input) })
  if (!response.ok) throw new Error(adminApiError('Inventory purchase requisition', response.status))
  return response.json()
}

export async function submitInventoryPurchaseRequisition(requisitionId: string, sessionId?: string | null): Promise<InventoryPurchaseRequisition> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/purchase-requisitions/${requisitionId}/submit`, { method: 'POST', headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'), body: '{}' })
  if (!response.ok) throw new Error(adminApiError('Inventory purchase requisition submit', response.status))
  return response.json()
}

export async function decideInventoryPurchaseRequisition(requisitionId: string, decision: 'approve' | 'reject', input: InventoryPurchaseRequisitionDecisionInput, sessionId?: string | null): Promise<InventoryPurchaseRequisition> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/purchase-requisitions/${requisitionId}/decisions/${decision}`, { method: 'POST', headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'), body: JSON.stringify(input) })
  if (!response.ok) throw new Error(adminApiError(`Inventory purchase requisition ${decision}`, response.status))
  return response.json()
}

export async function createInventoryCountReconciliation(
  input: InventoryCountReconciliationCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryCountReconciliationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/inventory/count-reconciliations`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory count reconciliation', response.status))
  }
  return response.json()
}

export async function getInventoryActivityReport(
  filters: { fromDate?: string; toDate?: string; facilityId?: string },
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<InventoryActivityReportResponse> {
  const params = new URLSearchParams()
  if (filters.fromDate?.trim()) params.set('from', filters.fromDate.trim())
  if (filters.toDate?.trim()) params.set('to', filters.toDate.trim())
  if (filters.facilityId?.trim()) params.set('facilityId', filters.facilityId.trim())
  const response = await fetch(`${apiBaseUrl}/api/inventory/activity?${params.toString()}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory activity report', response.status))
  }
  return response.json()
}

export async function getInventoryActivityCsv(
  filters: { fromDate?: string; toDate?: string; facilityId?: string },
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<string> {
  const params = new URLSearchParams()
  if (filters.fromDate?.trim()) params.set('from', filters.fromDate.trim())
  if (filters.toDate?.trim()) params.set('to', filters.toDate.trim())
  if (filters.facilityId?.trim()) params.set('facilityId', filters.facilityId.trim())
  const response = await fetch(`${apiBaseUrl}/api/inventory/activity/export?${params.toString()}`, {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(adminApiError('Inventory activity export', response.status))
  }
  return response.text()
}

export function getOperationalReportsCsvUrl() {
  return `${apiBaseUrl}/api/reports/operational/export`
}

export async function getOperationalReportsCsv(sessionId?: string | null, signal?: AbortSignal): Promise<string> {
  const response = await fetch(getOperationalReportsCsvUrl(), {
    headers: buildLegacyEhrSessionHeaders(sessionId),
    signal,
  })
  if (!response.ok) {
    throw new Error(
      adminApiError(
        'Operational reports CSV export',
        response.status,
        'Patient Report access',
        'an active Legacy EHR session',
      ),
    )
  }

  return response.text()
}
