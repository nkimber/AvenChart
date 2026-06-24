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
