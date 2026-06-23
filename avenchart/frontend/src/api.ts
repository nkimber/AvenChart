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

export type PrescriptionListItem = {
  id: string
  drug: string
  dosage?: string | null
  quantity?: string | null
  route?: string | null
  rxNormCode?: string | null
  diagnosis?: string | null
  startDate?: string | null
  endDate?: string | null
  refills: number
  active: number
  note?: string | null
  encounter?: number | null
  providerName?: string | null
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
  immunizations: ImmunizationListItem[]
  prescriptions: PrescriptionListItem[]
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

export type ClinicalPrescriptionCreateInput = {
  patientId: string
  providerId?: number | null
  startDate: string
  drug: string
  rxNormCode?: string | null
  dosage: string
  quantity: string
  route?: string | null
  refills: number
  note: string
  diagnosis: string
}

export type ClinicalPrescriptionDeactivateInput = {
  endDate: string
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

export type BillingPaymentCreateInput = {
  patientId: string
  encounter: number
  payerId: number
  payerName?: string | null
  payerType: number
  reference: string
  postDate: string
  checkDate?: string | null
  depositDate?: string | null
  paymentType: string
  paymentMethod: string
  codeType?: string | null
  code?: string | null
  modifier?: string | null
  memo: string
  payAmount: number
  adjustmentAmount: number
  accountCode?: string | null
  reasonCode?: string | null
  payerClaimNumber?: string | null
}

export type BillingPaymentMutationResponse = {
  id: string
  sessionId: number
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

export type AdministrationDirectoryResponse = {
  datasetId: string
  datasetVersion: string
  counts: AdministrationDirectoryCounts
  users: AdministrationUserItem[]
  facilities: AdministrationFacilityItem[]
  accessControl: AdministrationAccessControlSummary
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
    throw new Error(appointmentApiError('Appointment create', response.status))
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

export async function createBillingPaymentPosting(
  input: BillingPaymentCreateInput,
  sessionId?: string | null,
  signal?: AbortSignal,
): Promise<BillingPaymentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/payments`, {
    method: 'POST',
    headers: buildLegacyEhrSessionHeaders(sessionId, 'application/json'),
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(billingApiError('Billing payment posting create', response.status))
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
