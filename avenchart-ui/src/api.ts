// Minimal client for the existing avenchart backend API.
// This app does not own or modify that backend — it only calls the same
// endpoints that avenchart/frontend already uses for sign-in.

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5001'

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

export async function login(input: AuthLoginInput, signal?: AbortSignal): Promise<AuthLoginResponse> {
  const response = await fetch(`${apiBaseUrl}/api/auth/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Login request failed with ${response.status}`)
  }
  return response.json()
}

export async function getCurrentSession(sessionId: string, signal?: AbortSignal): Promise<AuthSessionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/auth/session`, {
    headers: { 'X-Legacy EHR-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Session check failed with ${response.status}`)
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
    throw new Error(`Patient portal login request failed with ${response.status}`)
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

// --- Patient portal home dashboard --------------------------------------
// The types and endpoints below mirror avenchart/frontend/src/api.ts
// (same backend, hand-copied rather than shared, per this app's "independent
// client" design decision). Only the fields the landing page renders are kept.

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

export type PatientPortalHomeSummaryResponse = {
  authenticated: boolean
  sessionId?: string | null
  displayName: string
  messages: PatientPortalHomeMessageSummary
  upcomingAppointmentCount: number
  upcomingAppointments: PatientPortalHomeAppointmentSummary[]
  failureReason?: string | null
  sessionSource: string
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

export type PatientPortalMessageItem = {
  id: string
  date: string
  title: string
  body: string
  status: string
  senderName: string
  recipientName: string
}

export type PatientPortalMessagesResponse = {
  authenticated: boolean
  messageCount: number
  messages: PatientPortalMessageItem[]
  failureReason?: string | null
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

export type PatientPortalComposeMessageInput = {
  title: string
  body: string
}

export type PatientPortalComposeMessageResponse = {
  authenticated: boolean
  created: boolean
  recipientName: string
  sentMessage?: PatientPortalMessageItem | null
  failureReason?: string | null
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

export type PatientPortalMessageThreadResponse = {
  authenticated: boolean
  messageId: string
  threadId: number
  anchorMessage?: PatientPortalMessageItem | null
  threadMessageCount: number
  threadMessages: PatientPortalMessageItem[]
  failureReason?: string | null
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

export type PatientPortalReplyMessageInput = {
  body?: string | null
}

export type PatientPortalReplyMessageResponse = {
  authenticated: boolean
  created: boolean
  originalMessageId: string
  sentMessage?: PatientPortalMessageItem | null
  failureReason?: string | null
}

export async function replyToPatientPortalMessage(
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

export type PatientPortalReadMessageResponse = {
  authenticated: boolean
  markedRead: boolean
  messageId: string
  message?: PatientPortalMessageItem | null
  failureReason?: string | null
}

export async function markPatientPortalMessageRead(
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
    throw new Error(`Patient portal message read failed with ${response.status}`)
  }
  return response.json()
}

export type PatientPortalDocumentItem = {
  id: number
  categoryName: string
  name: string
  docDate: string
  sizeBytes?: number | null
  canDownload: boolean
}

export type PatientPortalDocumentsResponse = {
  authenticated: boolean
  documentCount: number
  documents: PatientPortalDocumentItem[]
  failureReason?: string | null
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

export type PatientPortalDocumentsDownloadInput = {
  documentIds: number[]
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
    throw new Error(`Patient portal document download failed with ${response.status}`)
  }
  return response.blob()
}

export type PatientPortalLabResultItem = {
  id: string
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
  reportStatus?: string | null
  resultCount: number
  results: PatientPortalLabResultItem[]
}

export type PatientPortalLabOrderItem = {
  id: string
  orderDate: string
  procedureName: string
  orderStatus?: string | null
  reportCount: number
  resultCount: number
  reports: PatientPortalLabReportItem[]
}

export type PatientPortalLabResultsResponse = {
  authenticated: boolean
  orderCount: number
  orders: PatientPortalLabOrderItem[]
  failureReason?: string | null
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
  endDate?: string | null
  dosage?: string | null
  quantity?: string | null
  route?: string | null
  note?: string | null
}

export type PatientPortalClinicalSummaryResponse = {
  authenticated: boolean
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

export type PatientPortalAppointmentCategoryOption = {
  id: number
  name: string
  durationMinutes: number
}

export type PatientPortalAppointmentProviderOption = {
  id: number
  displayName: string
}

export type PatientPortalAppointmentFacilityOption = {
  id: number
  name: string
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
  categories: PatientPortalAppointmentCategoryOption[]
  providers: PatientPortalAppointmentProviderOption[]
  facilities: PatientPortalAppointmentFacilityOption[]
  defaults: PatientPortalAppointmentRequestDefaults
  failureReason?: string | null
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
  title: string
  body: string
}

export type PatientPortalAppointmentRequestResponse = {
  authenticated: boolean
  created: boolean
  appointment?: PatientPortalHomeAppointmentSummary | null
  reminder?: PatientPortalAppointmentReminder | null
  failureReason?: string | null
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

export async function downloadPatientPortalGeneratedMedicalReportPdf(
  sessionId: string,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/medical-report/pdf`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      'X-Legacy EHR-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify({}),
    signal,
  })
  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(errorText || `Patient portal medical report PDF download failed with ${response.status}`)
  }
  return response.blob()
}

// ─── Clinician API ───────────────────────────────────────────────────────────

function clinicianHeaders(sessionId: string): Record<string, string> {
  return { 'X-Legacy EHR-Session': sessionId, 'content-type': 'application/json' }
}

async function clinicianGet<T>(sessionId: string, path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { 'X-Legacy EHR-Session': sessionId },
    signal,
  })
  if (!response.ok) throw new Error(`GET ${path} failed with ${response.status}`)
  return response.json()
}

async function clinicianPost<T>(sessionId: string, path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'POST',
    headers: clinicianHeaders(sessionId),
    body: JSON.stringify(body),
    signal,
  })
  if (!response.ok) throw new Error(`POST ${path} failed with ${response.status}`)
  return response.json()
}

async function clinicianPut<T>(sessionId: string, path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'PUT',
    headers: clinicianHeaders(sessionId),
    body: JSON.stringify(body),
    signal,
  })
  if (!response.ok) throw new Error(`PUT ${path} failed with ${response.status}`)
  return response.json()
}

// ── Patients ──────────────────────────────────────────────────────────────────

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
  phone?: string | null
  phoneCell?: string | null
  email?: string | null
  primaryProviderName?: string | null
  facilityName?: string | null
  counts: PatientActivityCounts
}

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
  subscriberLastName?: string | null
}

export type PatientCareTeamSummary = {
  primaryProviderId?: number | null
  primaryProviderName?: string | null
  members?: Array<{ displayName: string; role?: string | null }> | null
}

export type PatientChartSummary = {
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
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  email?: string | null
  phone?: string | null
  phoneCell?: string | null
  maritalStatus?: string | null
  occupation?: string | null
  race?: string | null
  ethnicity?: string | null
  portalEnabled: boolean
  registrationDate: string
  deceasedDate?: string | null
  primaryProviderName?: string | null
  facilityName?: string | null
  careTeam?: PatientCareTeamSummary | null
  insurance: PatientInsuranceItem[]
  counts: PatientActivityCounts
  nextAppointment?: PatientTimelineItem | null
  latestEncounter?: PatientTimelineItem | null
}

export type PatientSearchResponse = {
  totalMatches: number
  patients: PatientListItem[]
}

export async function searchPatients(
  sessionId: string,
  params: { search?: string; limit?: number },
  signal?: AbortSignal,
): Promise<PatientSearchResponse> {
  const q = new URLSearchParams()
  if (params.search) q.set('search', params.search)
  if (params.limit) q.set('limit', String(params.limit))
  return clinicianGet(sessionId, `/api/patients/?${q}`, signal)
}

export async function getPatientChartSummary(
  sessionId: string,
  canonicalId: string,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianGet(sessionId, `/api/patients/${canonicalId}`, signal)
}

// ── Appointments ──────────────────────────────────────────────────────────────

export type AppointmentListItem = {
  id: string
  patientId: string
  patientDisplayName: string
  pubpid: string
  date: string
  startTime: string
  durationMinutes: number
  title: string
  status?: string | null
  room?: string | null
  categoryName?: string | null
  providerName?: string | null
  facilityName?: string | null
  comments?: string | null
}

export type AppointmentSearchResponse = {
  totalMatches: number
  appointments: AppointmentListItem[]
}

export async function searchAppointments(
  sessionId: string,
  params: { fromDate?: string; toDate?: string; patientId?: string; providerId?: number; limit?: number },
  signal?: AbortSignal,
): Promise<AppointmentSearchResponse> {
  const q = new URLSearchParams()
  if (params.fromDate) q.set('fromDate', params.fromDate)
  if (params.toDate) q.set('toDate', params.toDate)
  if (params.patientId) q.set('patientId', params.patientId)
  if (params.providerId) q.set('providerId', String(params.providerId))
  if (params.limit) q.set('limit', String(params.limit))
  return clinicianGet(sessionId, `/api/appointments/?${q}`, signal)
}

export async function updateAppointmentStatus(
  sessionId: string,
  appointmentId: string,
  status: string,
  signal?: AbortSignal,
): Promise<void> {
  await clinicianPut(sessionId, `/api/appointments/${appointmentId}/status`, { status }, signal)
}

// ── Encounters ────────────────────────────────────────────────────────────────

export type EncounterListItem = {
  id: number
  encounter: number
  patientId: string
  patientDisplayName: string
  date: string
  reason?: string | null
  diagnosisCode?: string | null
  diagnosisText?: string | null
  providerName?: string | null
  facilityName?: string | null
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

export type EncounterDiagnosisCode = {
  code: string
  description?: string | null
  billingLineCount: number
}

export type EncounterDetail = {
  id: number
  encounter: number
  patientId: string
  patientDisplayName: string
  date: string
  dateTime: string
  reason?: string | null
  diagnosisCode?: string | null
  diagnosisText?: string | null
  providerName?: string | null
  facilityName?: string | null
  sensitivity?: string | null
  billingNote?: string | null
  vitals?: EncounterVitals | null
  soapNote?: EncounterSoapNote | null
  billingLineCount: number
  diagnosisCodes: EncounterDiagnosisCode[]
}

export type EncounterSearchResponse = {
  totalMatches: number
  encounters: EncounterListItem[]
}

export async function searchEncounters(
  sessionId: string,
  params: { patientId?: string; fromDate?: string; limit?: number },
  signal?: AbortSignal,
): Promise<EncounterSearchResponse> {
  const q = new URLSearchParams()
  if (params.patientId) q.set('patientId', params.patientId)
  if (params.fromDate) q.set('fromDate', params.fromDate)
  if (params.limit) q.set('limit', String(params.limit))
  return clinicianGet(sessionId, `/api/encounters/?${q}`, signal)
}

export async function getEncounterDetail(
  sessionId: string,
  encounterId: number,
  signal?: AbortSignal,
): Promise<EncounterDetail> {
  return clinicianGet(sessionId, `/api/encounters/${encounterId}`, signal)
}

// ── Clinical Lists ────────────────────────────────────────────────────────────

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
  activity: number
}

export type MedicationListItem = {
  id: string
  title: string
  diagnosis?: string | null
  date?: string | null
  activity: number
}

export type ImmunizationListItem = {
  id: number
  vaccine: string
  administeredAt?: string | null
  manufacturer?: string | null
  lotNumber?: string | null
}

export type PrescriptionListItem = {
  id: string
  drug: string
  dosage?: string | null
  quantity?: string | null
  route?: string | null
  startDate?: string | null
  endDate?: string | null
  active: number
  providerName?: string | null
}

export type ClinicalListsResponse = {
  patientId: string
  patientDisplayName: string
  problems: ProblemListItem[]
  allergies: AllergyListItem[]
  medications: MedicationListItem[]
  immunizations: ImmunizationListItem[]
  prescriptions: PrescriptionListItem[]
}

export async function getClinicalLists(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<ClinicalListsResponse> {
  return clinicianGet(sessionId, `/api/clinical-lists/${patientId}`, signal)
}

// ── Messages ──────────────────────────────────────────────────────────────────

export type PatientMessageItem = {
  id: string
  date?: string | null
  title?: string | null
  body?: string | null
  status?: string | null
  assignedTo?: string | null
  portalRelation?: string | null
  deleted: number
}

export type PatientMessagesResponse = {
  patientId: string
  patientDisplayName: string
  portalEnabled: boolean
  messages: PatientMessageItem[]
}

export async function getPatientMessages(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientMessagesResponse> {
  return clinicianGet(sessionId, `/api/messages/${patientId}`, signal)
}

export async function replyToPatientMessage(
  sessionId: string,
  messageId: string,
  body: { body: string; assignedTo: string },
  signal?: AbortSignal,
): Promise<PatientMessagesResponse> {
  return clinicianPost(sessionId, `/api/messages/${messageId}/reply`, body, signal)
}

export async function updatePatientMessageStatus(
  sessionId: string,
  messageId: string,
  body: { status: string; body: string },
  signal?: AbortSignal,
): Promise<PatientMessagesResponse> {
  return clinicianPut(sessionId, `/api/messages/${messageId}/status`, body, signal)
}

// ── Documents ─────────────────────────────────────────────────────────────────

export type PatientDocumentItem = {
  id: number
  categoryName: string
  name: string
  docDate: string
  mimetype?: string | null
  sizeBytes?: number | null
  canDownload: boolean
}

export type PatientDocumentsResponse = {
  patientId: string
  patientDisplayName: string
  documents: PatientDocumentItem[]
}

export async function getPatientDocuments(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientDocumentsResponse> {
  return clinicianGet(sessionId, `/api/documents/${patientId}`, signal)
}

// ── Procedures / Lab Queue ────────────────────────────────────────────────────

export type ProcedureReportQueueItem = {
  reportId: number
  orderId: number
  patientId: string
  pubpid: string
  patientDisplayName: string
  orderDate: string
  providerName?: string | null
  labName?: string | null
  procedureCode?: string | null
  procedureName?: string | null
  reportDate: string
  reportStatus?: string | null
  reviewStatus?: string | null
  reviewedBy?: string | null
  notes?: string | null
}

export type ProcedureReportQueueResponse = {
  totalReports: number
  unreviewedReports: number
  reports: ProcedureReportQueueItem[]
}

export async function getProcedureReportQueue(
  sessionId: string,
  params?: { status?: string; limit?: number },
  signal?: AbortSignal,
): Promise<ProcedureReportQueueResponse> {
  const q = new URLSearchParams()
  if (params?.status) q.set('status', params.status)
  if (params?.limit) q.set('limit', String(params.limit))
  return clinicianGet(sessionId, `/api/procedures/report-review-queue?${q}`, signal)
}

export type ProcedureOrderQueueItem = {
  orderId: number
  patientId: string
  pubpid: string
  patientDisplayName: string
  orderDate: string
  providerName?: string | null
  labName?: string | null
  procedureCode?: string | null
  procedureName?: string | null
  orderStatus?: string | null
}

export type ProcedureOrderQueueResponse = {
  totalOrders: number
  readyToSendOrders: number
  reports: ProcedureOrderQueueItem[]
}

export async function getProcedureOrderQueue(
  sessionId: string,
  params?: { status?: string; limit?: number },
  signal?: AbortSignal,
): Promise<ProcedureOrderQueueResponse> {
  const q = new URLSearchParams()
  if (params?.status) q.set('status', params.status)
  if (params?.limit) q.set('limit', String(params.limit))
  return clinicianGet(sessionId, `/api/procedures/order-queue?${q}`, signal)
}

// ── Operational Reports ───────────────────────────────────────────────────────

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
  displayName: string
  encounters: number
  billingTotal: number
}

export type FacilityActivityReportItem = {
  code: string
  name: string
  appointments: number
  encounters: number
  billingTotal: number
}

export type ClinicalConditionReportItem = {
  title: string
  diagnosis: string
  patients: number
}

export type OperationalReportsResponse = {
  asOfDate: string
  currentYear: number
  counts: OperationalReportCounts
  providerActivity: ProviderActivityReportItem[]
  facilityActivity: FacilityActivityReportItem[]
  clinicalConditions: ClinicalConditionReportItem[]
}

export async function getOperationalReports(
  sessionId: string,
  signal?: AbortSignal,
): Promise<OperationalReportsResponse> {
  return clinicianGet(sessionId, '/api/reports/operational', signal)
}

// ── Administration ────────────────────────────────────────────────────────────

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
  facilityName?: string | null
  email?: string | null
  npi?: string | null
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

export type AdministrationAccessGroupItem = {
  id: number
  value: string
  name: string
  permissionCount: number
}

export type AdministrationDirectoryCounts = {
  users: number
  providers: number
  facilities: number
  accessGroups: number
  accessGroupPermissions: number
  accessUserMemberships: number
}

export type AdministrationDirectoryResponse = {
  counts: AdministrationDirectoryCounts
  users: AdministrationUserItem[]
  facilities: AdministrationFacilityItem[]
  accessControl: {
    groups: AdministrationAccessGroupItem[]
  }
}

export async function getAdministrationDirectory(
  sessionId: string,
  signal?: AbortSignal,
): Promise<AdministrationDirectoryResponse> {
  return clinicianGet(sessionId, '/api/administration/directory', signal)
}

export async function getLoginAudit(
  sessionId: string,
  limit?: number,
  signal?: AbortSignal,
): Promise<{ entries: Array<{ username: string; at: string; sourceIp?: string | null; success: boolean }> }> {
  const q = limit ? `?limit=${limit}` : ''
  return clinicianGet(sessionId, `/api/auth/login-audit${q}`, signal)
}

// ── Write helpers ─────────────────────────────────────────────────────────────

async function clinicianDelete(sessionId: string, path: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'DELETE',
    headers: clinicianHeaders(sessionId),
    signal,
  })
  if (!response.ok) throw new Error(`DELETE ${path} failed with ${response.status}`)
}

// ── Encounter mutations ───────────────────────────────────────────────────────

export type EncounterCreateInput = {
  patientId: string
  dateTime: string
  reason: string
  providerId?: number | null
  facilityId?: number | null
  sensitivity?: string | null
}

export async function createEncounter(
  sessionId: string,
  body: EncounterCreateInput,
  signal?: AbortSignal,
): Promise<EncounterDetail> {
  return clinicianPost(sessionId, '/api/encounters/', body, signal)
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

export async function createEncounterVitals(
  sessionId: string,
  encounterId: number,
  body: EncounterVitalsCreateInput,
  signal?: AbortSignal,
): Promise<{ id: number; detail: EncounterDetail }> {
  return clinicianPost(sessionId, `/api/encounters/${encounterId}/vitals`, body, signal)
}

export type EncounterSoapNoteCreateInput = {
  dateTime: string
  subjective?: string | null
  objective?: string | null
  assessment?: string | null
  plan?: string | null
}

export async function createEncounterSoapNote(
  sessionId: string,
  encounterId: number,
  body: EncounterSoapNoteCreateInput,
  signal?: AbortSignal,
): Promise<{ id: number; detail: EncounterDetail }> {
  return clinicianPost(sessionId, `/api/encounters/${encounterId}/soap-notes`, body, signal)
}

export async function signEncounter(
  sessionId: string,
  encounterId: number,
  body: { signerUsername: string; signedAt: string; isLock: boolean; amendment?: string | null },
  signal?: AbortSignal,
): Promise<{ id: number; detail: EncounterDetail }> {
  return clinicianPut(sessionId, `/api/encounters/${encounterId}/sign`, body, signal)
}

// ── Clinical list mutations ───────────────────────────────────────────────────

export type ClinicalListMutationResponse = {
  id: string
  detail: ClinicalListsResponse
}

export type CreateProblemInput = {
  patientId: string
  title: string
  dateTime: string
  diagnosis?: string | null
  comments: string
}

export async function createProblem(
  sessionId: string,
  body: CreateProblemInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPost(sessionId, '/api/clinical-lists/problems', body, signal)
}

export async function deactivateProblem(
  sessionId: string,
  problemId: string,
  comments: string,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(sessionId, `/api/clinical-lists/problems/${problemId}/deactivate`, { comments }, signal)
}

export async function deleteProblem(
  sessionId: string,
  problemId: string,
  signal?: AbortSignal,
): Promise<void> {
  return clinicianDelete(sessionId, `/api/clinical-lists/problems/${problemId}`, signal)
}

export type CreateAllergyInput = {
  patientId: string
  title: string
  dateTime: string
  comments: string
  reaction: string
  severity: string
  listOptionId?: string | null
}

export async function createAllergy(
  sessionId: string,
  body: CreateAllergyInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPost(sessionId, '/api/clinical-lists/allergies', body, signal)
}

export async function deactivateAllergy(
  sessionId: string,
  allergyId: string,
  comments: string,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(sessionId, `/api/clinical-lists/allergies/${allergyId}/deactivate`, { comments }, signal)
}

export type CreateMedicationInput = {
  patientId: string
  title: string
  dateTime: string
  diagnosis?: string | null
  comments: string
}

export async function createMedication(
  sessionId: string,
  body: CreateMedicationInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPost(sessionId, '/api/clinical-lists/medications', body, signal)
}

export async function deactivateMedication(
  sessionId: string,
  medicationId: string,
  comments: string,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(sessionId, `/api/clinical-lists/medications/${medicationId}/deactivate`, { comments }, signal)
}

export type CreatePrescriptionInput = {
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

export async function createPrescription(
  sessionId: string,
  body: CreatePrescriptionInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPost(sessionId, '/api/clinical-lists/prescriptions', body, signal)
}

export async function deactivatePrescription(
  sessionId: string,
  prescriptionId: string,
  body: { endDate: string; note: string },
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(sessionId, `/api/clinical-lists/prescriptions/${prescriptionId}/deactivate`, body, signal)
}

// ── Lab report sign ───────────────────────────────────────────────────────────

export async function signLabReport(
  sessionId: string,
  reportId: number,
  body: { reviewedBy: string; reviewedAt: string },
  signal?: AbortSignal,
): Promise<unknown> {
  return clinicianPut(sessionId, `/api/procedures/reports/${reportId}/sign`, body, signal)
}

// ── Message creation ──────────────────────────────────────────────────────────

export type CreatePatientMessageInput = {
  patientId: string
  title: string
  body: string
  assignedTo?: string | null
}

export async function createPatientMessage(
  sessionId: string,
  input: CreatePatientMessageInput,
  signal?: AbortSignal,
): Promise<PatientMessageItem> {
  return clinicianPost(sessionId, '/api/messages/', input, signal)
}
