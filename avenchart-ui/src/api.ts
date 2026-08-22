// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

// Compatibility barrel for the existing backend API. Domain modules migrate
// incrementally while every request shares this governed transport.
import {
  ApiRequestError,
  apiBaseUrl,
  apiFetch as fetch,
  requireSuccessfulResponse,
} from './api/transport.ts'
export {
  ApiRequestError,
  SESSION_INVALID_EVENT,
  isInvalidSessionError,
  isRequestCancellation,
  type ApiErrorKind,
  type ApiProblemDetails,
  type SessionScope,
} from './api/transport.ts'

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

export async function login(
  input: AuthLoginInput,
  signal?: AbortSignal,
): Promise<AuthLoginResponse> {
  const response = await fetch(`${apiBaseUrl}/api/auth/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  await requireSuccessfulResponse(response, 'Login request')
  return response.json()
}

export async function getCurrentSession(
  sessionId: string,
  signal?: AbortSignal,
): Promise<AuthSessionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/auth/session`, {
    headers: { 'X-AvenChart-Session': sessionId },
    signal,
  })
  await requireSuccessfulResponse(response, 'Session check', 'clinician')
  return response.json()
}

export async function logout(
  sessionId: string,
  signal?: AbortSignal,
): Promise<AuthSessionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/auth/logout`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ sessionId }),
    signal,
  })
  await requireSuccessfulResponse(response, 'Session logout', 'clinician')
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
  await requireSuccessfulResponse(response, 'Patient portal login request')
  return response.json()
}

export async function getPatientPortalSession(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalSessionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/session`, {
    headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
    signal,
  })
  await requireSuccessfulResponse(
    response,
    'Patient portal session check',
    'portal',
  )
  return response.json()
}

export async function endPatientPortalSession(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalSessionResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/session`, {
    method: 'DELETE',
    headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
    signal,
  })
  await requireSuccessfulResponse(
    response,
    'Patient portal session logout',
    'portal',
  )
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
    headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient portal home check failed with ${response.status}`)
  }
  return response.json()
}

export type PatientPortalMessageItem = {
  id: string
  type?: string
  date: string
  title: string
  body: string
  status: string
  assignedTo?: string
  senderId?: string
  senderName: string
  recipientId?: string
  recipientName: string
  mailChain?: number
  replyMailChain?: number
  portalRelation?: string | null
  isEncrypted?: boolean
  attachmentCount?: number
  attachments?: PatientPortalMessageAttachment[]
}

export type PatientPortalMessageAttachment = {
  id: string
  fileName: string
  contentType: string
  sizeBytes?: number | null
  source: string
}

export type PatientPortalMessagesResponse = {
  authenticated: boolean
  datasetId?: string
  datasetVersion?: string
  asOfDate?: string
  messageCount: number
  messages: PatientPortalMessageItem[]
  sentMessageCount?: number
  sentMessages?: PatientPortalMessageItem[]
  allMessageCount?: number
  allMessages?: PatientPortalMessageItem[]
  deletedMessageCount?: number
  deletedMessages?: PatientPortalMessageItem[]
  failureReason?: string | null
}

export async function getPatientPortalMessages(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMessagesResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/messages`, {
    headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(
      `Patient portal messages check failed with ${response.status}`,
    )
  }
  return response.json()
}

export type PatientPortalComposeMessageInput = {
  recipientId?: string
  title: string
  body: string
  attachments?: Array<{
    fileName: string
    contentType: string
    sizeBytes: number
    contentBase64: string
  }>
}

export type PatientPortalMessageComposeOptions = {
  authenticated: boolean
  defaultSubject: string
  subjectOptions: Array<{ value: string; label: string; default: boolean }>
  recipients: Array<{
    id: string
    displayName: string
    type: string
    active: boolean
    fallback: boolean
  }>
  failureReason?: string | null
}

export async function getPatientPortalMessageComposeOptions(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMessageComposeOptions> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/messages/compose-options`,
    {
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  if (!response.ok)
    throw new Error(
      `Patient portal compose options failed with ${response.status}`,
    )
  return response.json()
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
      'X-AvenChart-Patient-Portal-Session': sessionId,
    },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(
      `Patient portal message compose failed with ${response.status}`,
    )
  }
  return response.json()
}

export async function downloadPatientPortalMessageAttachment(
  sessionId: string,
  attachmentId: string,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/messages/attachments/${encodeURIComponent(attachmentId)}`,
    {
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  if (!response.ok)
    throw new Error(
      `Patient portal attachment download failed with ${response.status}`,
    )
  return response.blob()
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
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/messages/${messageId}/thread`,
    {
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(
      `Patient portal message thread check failed with ${response.status}`,
    )
  }
  return response.json()
}

export type PatientPortalReplyMessageInput = {
  body?: string | null
  attachments?: Array<{
    fileName: string
    contentType: string
    sizeBytes: number
    contentBase64: string
  }>
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
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/messages/${messageId}/reply`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-AvenChart-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify(input),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(
      `Patient portal message reply failed with ${response.status}`,
    )
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
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/messages/${messageId}/read`,
    {
      method: 'PUT',
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(
      `Patient portal message read failed with ${response.status}`,
    )
  }
  return response.json()
}

export type PatientPortalMessageLifecycleResponse = {
  authenticated: boolean
  archived?: boolean
  deleted?: boolean
  archivedMessageCount?: number
  deletedMessageCount?: number
  failureReason?: string | null
}

export async function archivePatientPortalMessages(
  sessionId: string,
  messageIds: number[],
  signal?: AbortSignal,
): Promise<PatientPortalMessageLifecycleResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/messages/archive`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-AvenChart-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify({ messageIds }),
      signal,
    },
  )
  if (!response.ok)
    throw new Error(`Patient portal archive failed with ${response.status}`)
  return response.json()
}

export async function deletePatientPortalMessage(
  sessionId: string,
  messageId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMessageLifecycleResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/messages/${encodeURIComponent(messageId)}`,
    {
      method: 'DELETE',
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  if (!response.ok)
    throw new Error(`Patient portal delete failed with ${response.status}`)
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
    headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(
      `Patient portal documents check failed with ${response.status}`,
    )
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
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/documents/download`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-AvenChart-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify(input),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(
      `Patient portal document download failed with ${response.status}`,
    )
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
    headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok) {
    throw new Error(
      `Patient portal lab results check failed with ${response.status}`,
    )
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
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/clinical-summary`,
    {
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(
      `Patient portal clinical summary check failed with ${response.status}`,
    )
  }
  return response.json()
}

export type PatientPortalPrescriptionRefillRequestInput = {
  requestDate?: string | null
  note?: string | null
}

export type PatientPortalPrescriptionRefillHistoryItem = {
  messageId: number
  threadId: number
  prescriptionId: string
  drug: string
  requestDate: string
  status:
    'pending' | 'clarification-requested' | 'approved' | 'denied' | 'completed'
  patientNote?: string | null
  staffResponse?: string | null
  updatedAt: string
  updatedBy: string
}

export type PatientPortalPrescriptionRefillHistoryResponse = {
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
  requestCount: number
  requests: PatientPortalPrescriptionRefillHistoryItem[]
  failureReason?: string | null
  sessionSource: string
}

export async function getPatientPortalPrescriptionRefillHistory(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalPrescriptionRefillHistoryResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/prescription-refill-requests`,
    {
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  await requireSuccessfulResponse(
    response,
    'Patient portal prescription refill history',
    'portal',
  )
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
        'X-AvenChart-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify(input),
      signal,
    },
  )
  if (!response.ok)
    throw new Error(
      `Patient portal refill request failed with ${response.status}`,
    )
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

export type PatientPortalAppointmentsResponse = {
  authenticated: boolean
  asOfDate: string
  upcomingAppointmentCount: number
  upcomingAppointments: PatientPortalHomeAppointmentSummary[]
  pastAppointmentCount: number
  pastAppointments: PatientPortalHomeAppointmentSummary[]
  failureReason?: string | null
}

export async function getPatientPortalAppointments(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalAppointmentsResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/appointments`,
    {
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  await requireSuccessfulResponse(
    response,
    'Patient portal appointments',
    'portal',
  )
  return response.json()
}

export async function getPatientPortalAppointmentRequestOptions(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalAppointmentRequestOptionsResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/appointments/request-options`,
    {
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  await requireSuccessfulResponse(
    response,
    'Patient portal appointment request options',
    'portal',
  )
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
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/appointments/requests`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-AvenChart-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify(input),
      signal,
    },
  )
  await requireSuccessfulResponse(
    response,
    'Patient portal appointment request',
    'portal',
  )
  return response.json()
}

export async function downloadPatientPortalGeneratedMedicalReportPdf(
  sessionId: string,
  input: PatientPortalMedicalReportGenerationInput = {},
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/medical-report/pdf`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-AvenChart-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify(input),
      signal,
    },
  )
  if (!response.ok) {
    const errorText = await response.text()
    throw new Error(
      errorText ||
        `Patient portal medical report PDF download failed with ${response.status}`,
    )
  }
  return response.blob()
}

export type PatientPortalMedicalReportSection = {
  id: string
  label: string
  group: string
  selected: boolean
}
export type PatientPortalMedicalReportIssue = {
  id: string
  typeLabel: string
  title: string
  status: string
}
export type PatientPortalMedicalReportEncounterForm = {
  id: string
  display: string
  encounter: number
}
export type PatientPortalMedicalReportEncounter = {
  encounter: number
  date: string
  display: string
  forms: PatientPortalMedicalReportEncounterForm[]
}
export type PatientPortalMedicalReportProcedureOrder = {
  id: string
  procedureName: string
  orderDate: string
  orderStatus?: string | null
  resultCount: number
}
export type PatientPortalMedicalReportGenerationInput = {
  sectionIds?: string[]
  procedureOrderIds?: string[]
  issueIds?: string[]
  encounterFormIds?: string[]
}
export type PatientPortalMedicalReportResponse = {
  authenticated: boolean
  sections: PatientPortalMedicalReportSection[]
  issues: PatientPortalMedicalReportIssue[]
  encounters: PatientPortalMedicalReportEncounter[]
  procedureOrders: PatientPortalMedicalReportProcedureOrder[]
  failureReason?: string | null
}
export type PatientPortalGeneratedMedicalReportResponse = {
  authenticated: boolean
  title: string
  generatedOn: string
  includedSectionIds: string[]
  includedIssueIds: string[]
  includedEncounterFormIds: string[]
  includedProcedureOrderIds: string[]
  pdfDownloadAvailable: boolean
  packageDownloadAvailable: boolean
  summaryLines: string[]
  failureReason?: string | null
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
export type PatientPortalGeneratedMedicalReportAuditResponse = {
  authenticated: boolean
  auditEventCount: number
  auditEvents: PatientPortalGeneratedMedicalReportAuditEvent[]
  failureReason?: string | null
}

export async function getPatientPortalMedicalReport(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalMedicalReportResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/medical-report`,
    {
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  if (!response.ok)
    throw new Error(
      `Patient portal medical report failed with ${response.status}`,
    )
  return response.json()
}

export async function generatePatientPortalMedicalReport(
  sessionId: string,
  input: PatientPortalMedicalReportGenerationInput,
  signal?: AbortSignal,
): Promise<PatientPortalGeneratedMedicalReportResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/medical-report/generate`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-AvenChart-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify(input),
      signal,
    },
  )
  if (!response.ok)
    throw new Error(
      `Patient portal medical report generation failed with ${response.status}`,
    )
  return response.json()
}

export async function getPatientPortalGeneratedMedicalReportAudit(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalGeneratedMedicalReportAuditResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/medical-report/audit`,
    {
      headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
      signal,
    },
  )
  await requireSuccessfulResponse(
    response,
    'Patient portal medical report history',
    'portal',
  )
  return response.json()
}

export async function downloadPatientPortalGeneratedMedicalReportPackage(
  sessionId: string,
  input: PatientPortalMedicalReportGenerationInput,
  signal?: AbortSignal,
): Promise<Blob> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/medical-report/package`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-AvenChart-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify(input),
      signal,
    },
  )
  if (!response.ok)
    throw new Error(
      `Patient portal medical report package failed with ${response.status}`,
    )
  return response.blob()
}

// ─── Clinician API ───────────────────────────────────────────────────────────

function clinicianHeaders(sessionId: string): Record<string, string> {
  return { 'X-AvenChart-Session': sessionId, 'content-type': 'application/json' }
}

async function clinicianGet<T>(
  sessionId: string,
  path: string,
  signal?: AbortSignal,
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { 'X-AvenChart-Session': sessionId },
    signal,
  })
  await requireSuccessfulResponse(response, `GET ${path}`, 'clinician')
  return response.json()
}

async function clinicianPost<T>(
  sessionId: string,
  path: string,
  body: unknown,
  signal?: AbortSignal,
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'POST',
    headers: clinicianHeaders(sessionId),
    body: JSON.stringify(body),
    signal,
  })
  await requireSuccessfulResponse(response, `POST ${path}`, 'clinician')
  return response.json()
}

async function clinicianPut<T>(
  sessionId: string,
  path: string,
  body: unknown,
  signal?: AbortSignal,
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'PUT',
    headers: clinicianHeaders(sessionId),
    body: JSON.stringify(body),
    signal,
  })
  await requireSuccessfulResponse(response, `PUT ${path}`, 'clinician')
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

export type PatientMergePreview = {
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

export type PatientMergeAuditPlan = {
  auditId: string
  plannedAt: string
  plannedBy: string
  status: string
  rationale?: string | null
  preview: PatientMergePreview
}

export type PatientMergeExecution = {
  executionId: string
  auditId: string
  status: 'Executed' | 'RolledBack'
  executedAt: string
  executedBy: string
  targetPatientId: string
  sourcePatientId: string
  movedRecords: Array<{ tableName: string; recordCount: number }>
  safeguards: string[]
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

export type PatientChartSummary = {
  canonicalId: string
  legacyPid: number
  pubpid: string
  administrationVersion: number
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
  phoneHome?: string | null
  phoneCell?: string | null
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
  lifecycleStatus: 'active' | 'retired' | string
  retiredAt?: string | null
  retiredBy?: string | null
  retirementReason?: string | null
  providerId?: number | null
  facilityId?: number | null
  primaryProviderName?: string | null
  facilityName?: string | null
  careTeam?: PatientCareTeamSummary | null
  insurance: PatientInsuranceItem[]
  duplicateCandidates: PatientDuplicateCandidate[]
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

export type PatientLifecycleHistoryItem = {
  eventId: string
  action: 'retired' | 'reactivated' | string
  priorStatus: 'active' | 'retired' | string
  resultingStatus: 'active' | 'retired' | string
  reason: string
  actor: string
  occurredAt: string
}

export type PatientLifecycleHistoryResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  currentStatus: 'active' | 'retired' | string
  retiredAt?: string | null
  retiredBy?: string | null
  retirementReason?: string | null
  eventCount: number
  events: PatientLifecycleHistoryItem[]
}

export async function getPatientLifecycleHistory(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientLifecycleHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/lifecycle-history`,
    signal,
  )
}

export async function transitionPatientLifecycle(
  sessionId: string,
  patientId: string,
  action: 'retire' | 'reactivate',
  reason: string,
): Promise<PatientChartSummary> {
  return clinicianPost(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/lifecycle/${action}`,
    { reason },
  )
}

export type PatientDeceasedStatusUpdate = {
  deceasedDate?: string | null
  deceasedReason?: string | null
  correctionReason: string
}

export type PatientDeceasedStatusHistoryItem = {
  eventId: string
  action: 'recorded' | 'corrected' | 'cleared' | string
  priorDeceasedDate?: string | null
  priorDeceasedReason?: string | null
  resultingDeceasedDate?: string | null
  resultingDeceasedReason?: string | null
  correctionReason: string
  actor: string
  occurredAt: string
}

export type PatientDeceasedStatusHistoryResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  currentDeceasedDate?: string | null
  currentDeceasedReason?: string | null
  eventCount: number
  events: PatientDeceasedStatusHistoryItem[]
}

export async function updatePatientDeceasedStatus(
  sessionId: string,
  patientId: string,
  input: PatientDeceasedStatusUpdate,
): Promise<PatientChartSummary> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/deceased-status`,
    input,
  )
}

export async function getPatientDeceasedStatusHistory(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientDeceasedStatusHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/deceased-status-history`,
    signal,
  )
}

export async function findPatientDuplicateCandidates(
  sessionId: string,
  input: {
    firstName?: string | null
    lastName?: string | null
    dateOfBirth?: string | null
    phone?: string | null
    email?: string | null
    excludePatientId?: string | null
    limit?: number | null
  },
  signal?: AbortSignal,
): Promise<PatientDuplicateSearchResponse> {
  const params = new URLSearchParams()
  const fields = [
    ['firstName', input.firstName],
    ['lastName', input.lastName],
    ['dateOfBirth', input.dateOfBirth],
    ['phone', input.phone],
    ['email', input.email],
    ['excludePatientId', input.excludePatientId],
  ] as const
  for (const [name, value] of fields) {
    if (value?.trim()) params.set(name, value.trim())
  }
  params.set('limit', String(input.limit ?? 10))
  return clinicianGet(
    sessionId,
    `/api/patients/duplicates?${params.toString()}`,
    signal,
  )
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
  reason?: string | null
}

export type PatientProviderAssignmentHistoryItem = {
  eventId: string
  fromProviderId?: number | null
  fromProviderName?: string | null
  fromFacilityId?: number | null
  fromFacilityName?: string | null
  toProviderId?: number | null
  toProviderName?: string | null
  toFacilityId?: number | null
  toFacilityName?: string | null
  reason: string
  actor: string
  occurredAt: string
}

export type PatientProviderAssignmentHistoryResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  currentProviderId?: number | null
  currentProviderName?: string | null
  currentFacilityId?: number | null
  currentFacilityName?: string | null
  eventCount: number
  returnedCount: number
  resultLimit: number
  events: PatientProviderAssignmentHistoryItem[]
}

export type PatientAdministrationHistoryItem = {
  eventId: string
  area: 'demographics' | 'contact' | 'insurance' | string
  action: 'created' | 'updated' | 'deleted' | string
  entityId?: string | null
  changedFields: string[]
  beforeValues: Record<string, string | null>
  afterValues: Record<string, string | null>
  actor: string
  occurredAt: string
}

export type PatientAdministrationHistoryResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  eventCount: number
  returnedCount: number
  resultLimit: number
  events: PatientAdministrationHistoryItem[]
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
}

export async function getPatientProviderAssignmentOptions(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientProviderAssignmentOptionsResponse> {
  return clinicianGet(sessionId, '/api/patients/provider-options', signal)
}

export async function getPatientCareTeamOptions(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientCareTeamOptionsResponse> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/care-team-options`,
    signal,
  )
}

export async function getPatientProviderAssignmentHistory(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientProviderAssignmentHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/provider-assignment-history`,
    signal,
  )
}

export async function getPatientAdministrationHistory(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientAdministrationHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/administration-history`,
    signal,
  )
}

export async function updatePatientGuardianContact(
  sessionId: string,
  patientId: string,
  guardianContact: PatientGuardianContactUpdate,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/guardian-contact`,
    guardianContact,
    signal,
  )
}

export async function updatePatientEmployer(
  sessionId: string,
  patientId: string,
  employer: PatientEmployerUpdate,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/employer`,
    employer,
    signal,
  )
}

export async function updatePatientProviderAssignment(
  sessionId: string,
  patientId: string,
  assignment: PatientProviderAssignmentUpdate,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/provider-assignment`,
    assignment,
    signal,
  )
}

export async function updatePatientCareTeam(
  sessionId: string,
  patientId: string,
  careTeam: PatientCareTeamUpdate,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/care-team`,
    careTeam,
    signal,
  )
}

export type PatientReferral = {
  id: string
  patientId: string
  encounterId?: number | null
  destination: string
  reason: string
  status: string
  externalReference?: string | null
  notes?: string | null
  requestedAt: string
  workflowVersion: number
  assignedTo: string
  assignedDisplayName: string
  dueAt?: string | null
  createdBy: string
  policyRevision: string
  createdAt: string
  updatedAt: string
  availableTransitions: ClinicalWorkflowTransitionOption[]
}

export type PatientReferralWorkflowEvent = PatientAuthorizationWorkflowEvent

export type PatientReferralWorkflowHistory = {
  referral: PatientReferral
  total: number
  events: PatientReferralWorkflowEvent[]
}

export type ReferralWorkQueueFilters = {
  status?: 'draft' | 'sent' | 'received' | 'closed' | 'cancelled' | 'all'
  assignedTo?: string
  overdueOnly?: boolean
  query?: string
  limit?: number
}

export type ReferralWorkQueueItem = {
  referral: PatientReferral
  patientDisplayName: string
  pubpid: string
  isOverdue: boolean
}

export type ReferralWorkQueueResponse = {
  total: number
  activeCount: number
  overdueCount: number
  items: ReferralWorkQueueItem[]
}

export async function getReferralWorkQueue(
  sessionId: string,
  filters: ReferralWorkQueueFilters = {},
  signal?: AbortSignal,
): Promise<ReferralWorkQueueResponse> {
  const query = new URLSearchParams()
  if (filters.status && filters.status !== 'all') query.set('status', filters.status)
  if (filters.assignedTo?.trim()) query.set('assignedTo', filters.assignedTo.trim())
  if (filters.overdueOnly) query.set('overdueOnly', 'true')
  if (filters.query?.trim()) query.set('query', filters.query.trim())
  if (filters.limit) query.set('limit', String(filters.limit))
  return clinicianGet(
    sessionId,
    `/api/clinical-workflows/referral-work-queue${query.size ? `?${query}` : ''}`,
    signal,
  )
}

export async function getPatientReferrals(
  sessionId: string,
  patientId: string,
): Promise<PatientReferral[]> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/referrals`,
  )
}
export async function createPatientReferral(
  sessionId: string,
  patientId: string,
  body: {
    encounterId?: number | null
    destination: string
    reason: string
    externalReference?: string
    notes?: string
    requestedAt?: string
    assignedTo?: string
    dueAt?: string
    workflowReason?: string
  },
): Promise<PatientReferral> {
  return clinicianPost(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/referrals`,
    body,
  )
}
export async function updatePatientReferralStatus(
  sessionId: string,
  patientId: string,
  referralId: string,
  body: {
    status: 'sent' | 'received' | 'closed' | 'cancelled'
    expectedVersion: number
    reasonCode: string
    reason: string
  },
): Promise<PatientReferral> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/referrals/${referralId}/status`,
    body,
  )
}

export async function updatePatientReferralAssignment(
  sessionId: string,
  patientId: string,
  referralId: string,
  body: {
    assignedTo: string
    dueAt?: string
    expectedVersion: number
    reasonCode: 'responsibility-transfer'
    reason: string
  },
): Promise<PatientReferral> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/referrals/${referralId}/assignment`,
    body,
  )
}

export async function getPatientReferralHistory(
  sessionId: string,
  patientId: string,
  referralId: string,
): Promise<PatientReferralWorkflowHistory> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/referrals/${referralId}/history`,
  )
}
export async function getPatientPrintableOutput(
  sessionId: string,
  patientId: string,
  output:
    | 'demographics'
    | 'chart-labels'
    | 'address-label'
    | 'referral'
    | 'fee-sheet',
  options: {
    referralId?: string
    encounterId?: number
    labelCount?: number
  } = {},
): Promise<string> {
  const query = new URLSearchParams()
  if (options.referralId) query.set('referralId', options.referralId)
  if (options.encounterId) query.set('encounterId', String(options.encounterId))
  if (options.labelCount) query.set('labelCount', String(options.labelCount))
  const response = await fetch(
    `${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/print/${output}?${query}`,
    {
      headers: { 'X-AvenChart-Session': sessionId },
    },
  )
  if (!response.ok)
    throw new Error(`Printable output failed with ${response.status}`)
  return response.text()
}
export type ClinicalWorkflowTransitionOption = {
  action: string
  fromState: string
  toState: string
  reasonCode: string
  label: string
  requiresAuthorizationNumber: boolean
}

export type ClinicalWorkflowAssignee = {
  staffId?: number | null
  username: string
  displayName: string
  role: string
}

export type ClinicalWorkflowAssigneesResponse = {
  policyRevision: string
  count: number
  assignees: ClinicalWorkflowAssignee[]
}

export type PatientAuthorization = {
  id: string
  patientId: string
  referralId?: string | null
  payer: string
  service: string
  status: string
  authorizationNumber?: string | null
  requestedAt: string
  expiresAt?: string | null
  workflowVersion: number
  assignedTo: string
  assignedDisplayName: string
  dueAt?: string | null
  createdBy: string
  policyRevision: string
  createdAt: string
  updatedAt: string
  availableTransitions: ClinicalWorkflowTransitionOption[]
}

export type PatientAuthorizationWorkflowEvent = {
  eventId: string
  workflowVersion: number
  action: string
  fromState?: string | null
  toState: string
  fromAssignedTo?: string | null
  toAssignedTo?: string | null
  reasonCode: string
  reason: string
  actor: string
  policyRevision: string
  occurredAt: string
}

export type PatientAuthorizationWorkflowHistory = {
  authorization: PatientAuthorization
  total: number
  events: PatientAuthorizationWorkflowEvent[]
}

export type AuthorizationWorkQueueFilters = {
  status?: 'draft' | 'submitted' | 'approved' | 'denied' | 'cancelled' | 'expired' | 'all'
  assignedTo?: string
  overdueOnly?: boolean
  expiringOnly?: boolean
  query?: string
  limit?: number
}

export type AuthorizationWorkQueueItem = {
  authorization: PatientAuthorization
  patientDisplayName: string
  pubpid: string
  isOverdue: boolean
  isExpiring: boolean
}

export type AuthorizationWorkQueueResponse = {
  total: number
  activeCount: number
  overdueCount: number
  expiringCount: number
  items: AuthorizationWorkQueueItem[]
}

export async function getAuthorizationWorkQueue(
  sessionId: string,
  filters: AuthorizationWorkQueueFilters = {},
  signal?: AbortSignal,
): Promise<AuthorizationWorkQueueResponse> {
  const query = new URLSearchParams()
  if (filters.status && filters.status !== 'all') query.set('status', filters.status)
  if (filters.assignedTo?.trim()) query.set('assignedTo', filters.assignedTo.trim())
  if (filters.overdueOnly) query.set('overdueOnly', 'true')
  if (filters.expiringOnly) query.set('expiringOnly', 'true')
  if (filters.query?.trim()) query.set('query', filters.query.trim())
  if (filters.limit) query.set('limit', String(filters.limit))
  return clinicianGet(sessionId, `/api/clinical-workflows/authorization-work-queue${query.size ? `?${query}` : ''}`, signal)
}

export async function getClinicalWorkflowAssignees(
  sessionId: string,
  signal?: AbortSignal,
): Promise<ClinicalWorkflowAssigneesResponse> {
  return clinicianGet(sessionId, '/api/clinical-workflows/assignees', signal)
}

export async function getPatientAuthorizations(
  sessionId: string,
  patientId: string,
): Promise<PatientAuthorization[]> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/authorizations`,
  )
}
export async function createPatientAuthorization(
  sessionId: string,
  patientId: string,
  body: {
    payer: string
    service: string
    expiresAt?: string
    assignedTo: string
    dueAt?: string
    reason: string
  },
): Promise<PatientAuthorization> {
  return clinicianPost(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/authorizations`,
    body,
  )
}
export async function updatePatientAuthorizationStatus(
  sessionId: string,
  patientId: string,
  authorizationId: string,
  body: {
    status: 'submitted' | 'approved' | 'denied' | 'expired' | 'cancelled'
    authorizationNumber?: string
    expectedVersion: number
    reasonCode: string
    reason: string
  },
): Promise<PatientAuthorization> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/authorizations/${authorizationId}/status`,
    body,
  )
}

export async function updatePatientAuthorizationAssignment(
  sessionId: string,
  patientId: string,
  authorizationId: string,
  body: {
    assignedTo: string
    dueAt?: string
    expectedVersion: number
    reasonCode: 'responsibility-transfer'
    reason: string
  },
): Promise<PatientAuthorization> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/authorizations/${authorizationId}/assignment`,
    body,
  )
}

export async function getPatientAuthorizationHistory(
  sessionId: string,
  patientId: string,
  authorizationId: string,
): Promise<PatientAuthorizationWorkflowHistory> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/authorizations/${authorizationId}/history`,
  )
}

export async function deletePatientAuthorizationTestFixture(
  sessionId: string,
  patientId: string,
  authorizationId: string,
): Promise<void> {
  return clinicianDelete(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/authorizations/${authorizationId}/test-fixture`,
  )
}

export type PatientRecordRequest = {
  requestId: string
  patientId: string
  legacyPid: number
  status: 'Open' | 'Completed'
  requestedAt: string
  requestedBy: string
  completedAt?: string | null
  completedBy?: string | null
}
export async function getPatientRecordRequests(
  sessionId: string,
  patientId: string,
): Promise<PatientRecordRequest[]> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/record-requests`,
  )
}
export async function createPatientRecordRequest(
  sessionId: string,
  patientId: string,
): Promise<PatientRecordRequest> {
  return clinicianPost(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/record-requests`,
    {},
  )
}
export async function completePatientRecordRequest(
  sessionId: string,
  patientId: string,
  requestId: string,
): Promise<PatientRecordRequest> {
  return clinicianPost(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/record-requests/${encodeURIComponent(requestId)}/complete`,
    {},
  )
}

export type PatientSdohDomainValue = { status: string; notes?: string | null }
export type PatientSdohGeneratedGoal = {
  domain: string
  description: string
  dueDate: string
}
export type PatientSdohGeneratedIntervention = {
  domain: string
  description: string
  reason: string
}
export type PatientSdohAssessment = {
  assessmentId: string
  patientId: string
  legacyPid: number
  assessmentDate: string
  screeningTool?: string | null
  assessor: string
  instrumentScore: number
  hungerQuestionOne?: string | null
  hungerQuestionTwo?: string | null
  hungerScore: number
  pregnancyStatus?: string | null
  pregnancyEdd?: string | null
  pregnancyIntent?: string | null
  postpartumStatus?: string | null
  postpartumEnd?: string | null
  disabilityStatus?: string | null
  disabilityStatusNotes?: string | null
  disabilityScale: Record<string, string>
  generatedGoals: PatientSdohGeneratedGoal[]
  generatedInterventions: PatientSdohGeneratedIntervention[]
  domains: Record<string, PatientSdohDomainValue>
  interventions?: string | null
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type PatientSdohAssessmentInput = {
  assessmentDate: string
  screeningTool?: string
  assessor?: string
  domains: Record<string, PatientSdohDomainValue>
  hungerQuestionOne?: string
  hungerQuestionTwo?: string
  pregnancyStatus?: string
  pregnancyEdd?: string
  pregnancyIntent?: string
  postpartumStatus?: string
  postpartumEnd?: string
  disabilityStatus?: string
  disabilityStatusNotes?: string
  disabilityScale?: Record<string, string>
  interventions?: string
}
export async function getPatientSdohAssessments(
  sessionId: string,
  patientId: string,
): Promise<PatientSdohAssessment[]> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/sdoh-assessments`,
  )
}
export async function createPatientSdohAssessment(
  sessionId: string,
  patientId: string,
  body: PatientSdohAssessmentInput,
): Promise<PatientSdohAssessment> {
  return clinicianPost(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/sdoh-assessments`,
    body,
  )
}
export async function updatePatientSdohAssessment(
  sessionId: string,
  patientId: string,
  assessmentId: string,
  body: PatientSdohAssessmentInput,
): Promise<PatientSdohAssessment> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/sdoh-assessments/${encodeURIComponent(assessmentId)}`,
    body,
  )
}

export async function getPatientMergePreview(
  sessionId: string,
  targetPatientId: string,
  sourcePatientId: string,
  signal?: AbortSignal,
): Promise<PatientMergePreview> {
  const query = new URLSearchParams({ targetPatientId, sourcePatientId })
  return clinicianGet(sessionId, `/api/patients/merge-preview?${query}`, signal)
}

export async function createPatientMergeAuditPlan(
  sessionId: string,
  body: {
    targetPatientId: string
    sourcePatientId: string
    rationale?: string | null
  },
  signal?: AbortSignal,
): Promise<PatientMergeAuditPlan> {
  return clinicianPost(sessionId, '/api/patients/merge-audits', body, signal)
}

export async function executePatientMerge(
  sessionId: string,
  auditId: string,
  signal?: AbortSignal,
): Promise<PatientMergeExecution> {
  return clinicianPost(
    sessionId,
    '/api/patients/merge-executions',
    { auditId },
    signal,
  )
}

export async function rollbackPatientMerge(
  sessionId: string,
  executionId: string,
  signal?: AbortSignal,
): Promise<PatientMergeExecution> {
  return clinicianPost(
    sessionId,
    '/api/patients/merge-executions/rollback',
    { executionId },
    signal,
  )
}

export async function updatePatientPortalAccountAccess(
  sessionId: string,
  patientId: string,
  portalEnabled: boolean,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/portal-account/access`,
    { portalEnabled },
    signal,
  )
}

export async function updatePatientPortalAccountReset(
  sessionId: string,
  patientId: string,
  oneTimeLinkPending: boolean,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/portal-account/reset`,
    { oneTimeLinkPending },
    signal,
  )
}

// ── Appointments ──────────────────────────────────────────────────────────────

export type AppointmentListItem = {
  id: string
  seriesRootId: string
  isRecurringSeries: boolean
  isVirtualOccurrence: boolean
  occurrenceNumber?: number | null
  patientId: string
  patientDisplayName: string
  pubpid: string
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
  reminderDue: boolean
  reminderStatus: string
  reminderChannel: string
  reminderContact?: string | null
  reminderLeadDays?: number | null
}

export type AppointmentSearchResponse = {
  totalMatches: number
  fromDate?: string | null
  toDate?: string | null
  appointments: AppointmentListItem[]
}

export type AppointmentSchedulingProviderOption = {
  id: number
  displayName: string
  facilityId?: number | null
  facilityName?: string | null
}

export type AppointmentSchedulingFacilityOption = {
  id: number
  name: string
  code?: string | null
}

export type AppointmentSchedulingOptionsResponse = {
  providers: AppointmentSchedulingProviderOption[]
  facilities: AppointmentSchedulingFacilityOption[]
}

export async function searchAppointments(
  sessionId: string,
  params: {
    fromDate?: string
    toDate?: string
    patientId?: string
    providerId?: number
    limit?: number
  },
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

export async function getAppointmentSchedulingOptions(
  sessionId: string,
  signal?: AbortSignal,
): Promise<AppointmentSchedulingOptionsResponse> {
  return clinicianGet(sessionId, '/api/appointments/scheduling-options', signal)
}

export type AppointmentWaitlistItem = {
  appointmentId: string
  patientId: string
  pubpid: string
  patientDisplayName: string
  date: string
  startTime: string
  endTime: string
  durationMinutes: number
  title: string
  status?: string | null
  categoryName?: string | null
  providerName?: string | null
  facilityName?: string | null
  room?: string | null
  reason?: string | null
  daysUntilRequestedSlot: number
  priority: string
  reminderCreated: boolean
  reminderId?: string | null
  reminderStatus?: string | null
  reminderAssignedTo?: string | null
}

export type AppointmentWaitlistResponse = {
  asOfDate: string
  totalWaiting: number
  items: AppointmentWaitlistItem[]
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
  templates: AppointmentReminderTemplateOption[]
}

export type AppointmentReminderDispatchResponse = {
  appointmentId: string
  dispatchId: string
  auditId: string
  dispatchedAt: string
  patientDisplayName: string
  appointmentDate: string
  startTime: string
  reminderStatus: string
  reminderChannel: string
  queueName: string
  dispatchStatus: string
  templateName: string
  retryAttempt: number
}

export type AppointmentReminderDispatchHistoryResponse = {
  eventCount: number
  entries: AppointmentReminderDispatchResponse[]
}

export async function getAppointmentWaitlist(
  sessionId: string,
  signal?: AbortSignal,
): Promise<AppointmentWaitlistResponse> {
  return clinicianGet(sessionId, '/api/appointments/waitlist', signal)
}

export async function getAppointmentReminderTemplates(
  sessionId: string,
  signal?: AbortSignal,
): Promise<AppointmentReminderTemplateCatalogResponse> {
  return clinicianGet(
    sessionId,
    '/api/appointments/reminders/templates',
    signal,
  )
}

export async function dispatchAppointmentReminder(
  sessionId: string,
  appointmentId: string,
  templateId?: string | null,
  signal?: AbortSignal,
): Promise<AppointmentReminderDispatchResponse> {
  return clinicianPost(
    sessionId,
    `/api/appointments/${encodeURIComponent(appointmentId)}/reminders/dispatch`,
    { templateId: templateId || null },
    signal,
  )
}

export async function retryAppointmentReminderDispatch(
  sessionId: string,
  appointmentId: string,
  signal?: AbortSignal,
): Promise<AppointmentReminderDispatchResponse> {
  return clinicianPost(
    sessionId,
    `/api/appointments/${encodeURIComponent(appointmentId)}/reminders/dispatch/retry`,
    {},
    signal,
  )
}

export async function getAppointmentReminderDispatchHistory(
  sessionId: string,
  appointmentId?: string,
  signal?: AbortSignal,
): Promise<AppointmentReminderDispatchHistoryResponse> {
  const suffix = appointmentId
    ? `?appointmentId=${encodeURIComponent(appointmentId)}`
    : ''
  return clinicianGet(
    sessionId,
    `/api/appointments/reminders/dispatch-history${suffix}`,
    signal,
  )
}

export async function updateAppointmentStatus(
  sessionId: string,
  appointmentId: string,
  status: string,
  signal?: AbortSignal,
): Promise<void> {
  await clinicianPut(
    sessionId,
    `/api/appointments/${appointmentId}/status`,
    { status },
    signal,
  )
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

export async function updateAppointment(
  sessionId: string,
  appointmentId: string,
  body: AppointmentUpdateInput,
  signal?: AbortSignal,
): Promise<AppointmentListItem> {
  return clinicianPut(
    sessionId,
    `/api/appointments/${appointmentId}`,
    body,
    signal,
  )
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

export async function rescheduleAppointmentOccurrence(
  sessionId: string,
  appointmentId: string,
  occurrenceDate: string,
  body: AppointmentOccurrenceRescheduleInput,
  signal?: AbortSignal,
): Promise<AppointmentListItem> {
  return clinicianPost(
    sessionId,
    `/api/appointments/${encodeURIComponent(appointmentId)}/occurrences/${encodeURIComponent(occurrenceDate)}/reschedule`,
    body,
    signal,
  )
}

export async function restoreAppointmentOccurrence(
  sessionId: string,
  appointmentId: string,
  occurrenceDate: string,
  signal?: AbortSignal,
): Promise<AppointmentListItem> {
  return clinicianPost(
    sessionId,
    `/api/appointments/${encodeURIComponent(appointmentId)}/recurrence-exceptions/${encodeURIComponent(occurrenceDate)}/restore`,
    undefined,
    signal,
  )
}

export async function deleteAppointment(
  sessionId: string,
  appointmentId: string,
  signal?: AbortSignal,
): Promise<void> {
  await clinicianDelete(sessionId, `/api/appointments/${appointmentId}`, signal)
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
export type FlowBoardResponse = { date: string; lanes: FlowBoardLane[] }
export async function getAppointmentFlowBoard(
  sessionId: string,
  date?: string,
  signal?: AbortSignal,
): Promise<FlowBoardResponse> {
  const suffix = date ? `?date=${encodeURIComponent(date)}` : ''
  return clinicianGet(
    sessionId,
    `/api/appointments/flow-board${suffix}`,
    signal,
  )
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
  expiryStatus?: string | null
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
export type InventoryMedicationLinkAuditEvent = {
  auditId: string
  priorRxNormCode: string | null
  newRxNormCode: string | null
  action: 'linked' | 'updated' | 'unlinked'
  changedBy: string
  changedAt: string
  reason: string | null
}
export type InventoryMedicationLinkHistoryResponse = {
  itemId: number
  events: InventoryMedicationLinkAuditEvent[]
}
export async function getInventoryMedicationCatalog(
  sessionId: string,
  signal?: AbortSignal,
): Promise<InventoryMedicationCatalogItem[]> {
  return clinicianGet(sessionId, '/api/inventory/medication-catalog', signal)
}
export async function updateInventoryMedicationLink(
  sessionId: string,
  itemId: number,
  rxNormCode: string,
  signal?: AbortSignal,
): Promise<InventoryMedicationLink> {
  return clinicianPut(
    sessionId,
    `/api/inventory/items/${itemId}/medication-link`,
    { rxNormCode },
    signal,
  )
}
export async function getInventoryMedicationLinkHistory(
  sessionId: string,
  itemId: number,
  signal?: AbortSignal,
): Promise<InventoryMedicationLinkHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/inventory/items/${itemId}/medication-link/history`,
    signal,
  )
}
export async function unlinkInventoryMedicationLink(
  sessionId: string,
  itemId: number,
  reason: string,
  signal?: AbortSignal,
): Promise<InventoryMedicationLinkHistoryResponse> {
  const path = `/api/inventory/items/${itemId}/medication-link`
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'DELETE',
    headers: {
      ...clinicianHeaders(sessionId),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ reason }),
    signal,
  })
  await requireSuccessfulResponse(response, `DELETE ${path}`, 'clinician')
  return response.json()
}
export type InventoryControlledLocation = {
  locationId: string
  facilityId: number
  facilityCode: string
  facilityName: string
  locationCode: string
  displayName: string
  dualAttestationRequired: boolean
  active: boolean
  updatedAt: string
  updatedBy: string
}
export type InventoryControlledSubstanceItem = {
  itemId: number
  itemCode: string
  name: string
  category: string
  unit: string
  scheduleCode: string
}
export type InventoryControlledSubstanceCatalogResponse = {
  locations: InventoryControlledLocation[]
  items: InventoryControlledSubstanceItem[]
}
export type InventoryControlledCountLine = {
  lineId: string
  lotId: number
  lotNumber: string
  itemCode: string
  expectedQuantity: number
  observedQuantity: number | null
  varianceQuantity: number | null
  discrepancyId: string | null
  discrepancyStatus: string | null
}
export type InventoryControlledCountSession = {
  sessionId: string
  locationId: string
  locationCode: string
  locationName: string
  countType: 'opening' | 'shift' | 'cycle' | 'closing'
  status: string
  movementLockActive: boolean
  reason: string
  startedBy: string
  startedAt: string
  submittedBy: string | null
  submittedAt: string | null
  counterUsername: string | null
  lines: InventoryControlledCountLine[]
}
export type InventoryControlledCountSessionSummary = Omit<
  InventoryControlledCountSession,
  'reason' | 'submittedBy' | 'counterUsername' | 'lines'
> & {
  lineCount: number
  discrepancyCount: number
  openDiscrepancyCount: number
}
export async function getInventoryControlledSubstanceCatalog(
  sessionId: string,
  signal?: AbortSignal,
): Promise<InventoryControlledSubstanceCatalogResponse> {
  return clinicianGet(sessionId, '/api/inventory/controlled-substances', signal)
}
export async function getInventoryControlledCountSessions(
  sessionId: string,
  limit = 30,
  signal?: AbortSignal,
): Promise<InventoryControlledCountSessionSummary[]> {
  return clinicianGet(
    sessionId,
    `/api/inventory/controlled-count-sessions?limit=${encodeURIComponent(String(limit))}`,
    signal,
  )
}
export async function getInventoryControlledCountSession(
  sessionId: string,
  countSessionId: string,
  signal?: AbortSignal,
): Promise<InventoryControlledCountSession> {
  return clinicianGet(
    sessionId,
    `/api/inventory/controlled-count-sessions/${countSessionId}`,
    signal,
  )
}
export async function createInventoryControlledCountSession(
  sessionId: string,
  input: {
    locationId: string
    countType: string
    movementLockActive: boolean
    reason: string
    idempotencyKey: string
  },
): Promise<InventoryControlledCountSession> {
  return clinicianPost(
    sessionId,
    '/api/inventory/controlled-count-sessions',
    input,
  )
}
export async function submitInventoryControlledCountSession(
  sessionId: string,
  countSessionId: string,
  input: {
    counterSessionId: string
    reason: string
    idempotencyKey: string
    observations: { lotId: number; observedQuantity: number }[]
  },
): Promise<InventoryControlledCountSession> {
  return clinicianPost(
    sessionId,
    `/api/inventory/controlled-count-sessions/${countSessionId}/submit`,
    input,
  )
}
export async function investigateInventoryControlledDiscrepancy(
  sessionId: string,
  discrepancyId: string,
  notes: string,
): Promise<InventoryControlledCountSession> {
  return clinicianPut(
    sessionId,
    `/api/inventory/controlled-count-discrepancies/${discrepancyId}/investigation`,
    { notes },
  )
}
export async function correctInventoryControlledDiscrepancy(
  sessionId: string,
  discrepancyId: string,
  input: { notes: string; idempotencyKey: string; witnessSessionId?: string },
): Promise<unknown> {
  return clinicianPost(
    sessionId,
    `/api/inventory/controlled-count-discrepancies/${discrepancyId}/corrections`,
    input,
  )
}
export async function closeInventoryControlledDiscrepancy(
  sessionId: string,
  discrepancyId: string,
  notes: string,
): Promise<InventoryControlledCountSession> {
  return clinicianPost(
    sessionId,
    `/api/inventory/controlled-count-discrepancies/${discrepancyId}/close`,
    { notes },
  )
}
export type InventoryCostPolicyDefinition = {
  method:
    | 'fifo'
    | 'weighted_average'
    | 'specific_identification'
    | 'practice_specific'
  currency: string
  taxTreatment: string
  freightTreatment: string
  landedCostTreatment: string
  roundingRule: 'half_up' | 'half_even' | 'truncate'
  backdatedEntryRule: 'prohibited' | 'restatement'
  effectiveDate: string
  approvalReference: string
  rationale: string
}
export type InventoryCostPolicy = {
  policyId: string
  scopeType: string
  definition: InventoryCostPolicyDefinition
  revision: number
  status: string
  activatedAt: string
  activatedBy: string
  supersededAt: string | null
  supersededBy: string | null
}
export type InventoryCostPolicyChangeRequest = {
  requestId: string
  proposedDefinition: InventoryCostPolicyDefinition
  baselinePolicyId: string | null
  baselineRevision: number | null
  reason: string
  status:
    'draft' | 'submitted' | 'approved' | 'rejected' | 'activated' | 'cancelled'
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type InventoryCostPolicyChangeRequestEvent = {
  eventId: number
  action: string
  note: string | null
  occurredAt: string
  username: string
}
export type InventoryCostPolicyCatalogResponse = {
  activePolicy: InventoryCostPolicy | null
  requests: InventoryCostPolicyChangeRequest[]
}
export type InventoryCostPolicyChangeRequestDetailResponse = {
  request: InventoryCostPolicyChangeRequest
  activePolicy: InventoryCostPolicy | null
  events: InventoryCostPolicyChangeRequestEvent[]
}
export async function getInventoryCostPolicies(
  sessionId: string,
  signal?: AbortSignal,
): Promise<InventoryCostPolicyCatalogResponse> {
  return clinicianGet(sessionId, '/api/inventory/cost-policies', signal)
}
export async function createInventoryCostPolicyChangeRequest(
  sessionId: string,
  input: { proposedDefinition: InventoryCostPolicyDefinition; reason: string },
): Promise<InventoryCostPolicyChangeRequestDetailResponse> {
  return clinicianPost(
    sessionId,
    '/api/inventory/cost-policy-change-requests',
    input,
  )
}
export async function getInventoryCostPolicyChangeRequest(
  sessionId: string,
  requestId: string,
): Promise<InventoryCostPolicyChangeRequestDetailResponse> {
  return clinicianGet(
    sessionId,
    `/api/inventory/cost-policy-change-requests/${requestId}`,
  )
}
export async function transitionInventoryCostPolicyChangeRequest(
  sessionId: string,
  requestId: string,
  action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel',
  input: { expectedVersion: number; note?: string },
): Promise<InventoryCostPolicyChangeRequestDetailResponse> {
  return clinicianPost(
    sessionId,
    `/api/inventory/cost-policy-change-requests/${requestId}/${action}`,
    input,
  )
}
export type InventoryAccountingIntegrationDecisionDefinition = {
  mode: 'external' | 'integration_accepted'
  financeOwner: string
  effectiveDate: string
  mappingReference: string | null
  reconciliationReference: string | null
  rationale: string
}
export type InventoryAccountingIntegrationDecision = {
  decisionId: string
  definition: InventoryAccountingIntegrationDecisionDefinition
  revision: number
  status: 'active' | 'superseded'
  activatedAt: string
  activatedBy: string
  supersededAt: string | null
  supersededBy: string | null
}
export type InventoryAccountingIntegrationChangeRequest = {
  requestId: string
  proposedDefinition: InventoryAccountingIntegrationDecisionDefinition
  baselineDecisionId: string | null
  baselineRevision: number | null
  reason: string
  status:
    | 'draft'
    | 'submitted'
    | 'approved'
    | 'rejected'
    | 'activated'
    | 'cancelled'
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type InventoryAccountingIntegrationChangeRequestEvent = {
  eventId: number
  action: string
  note: string | null
  occurredAt: string
  username: string
}
export type InventoryAccountingIntegrationCatalogResponse = {
  activeDecision: InventoryAccountingIntegrationDecision | null
  requests: InventoryAccountingIntegrationChangeRequest[]
}
export type InventoryAccountingIntegrationChangeRequestDetailResponse = {
  request: InventoryAccountingIntegrationChangeRequest
  activeDecision: InventoryAccountingIntegrationDecision | null
  events: InventoryAccountingIntegrationChangeRequestEvent[]
}
export async function getInventoryAccountingIntegrationDecision(
  sessionId: string,
  signal?: AbortSignal,
): Promise<InventoryAccountingIntegrationCatalogResponse> {
  return clinicianGet(
    sessionId,
    '/api/inventory/accounting-integration-decision',
    signal,
  )
}
export async function createInventoryAccountingIntegrationChangeRequest(
  sessionId: string,
  input: {
    proposedDefinition: InventoryAccountingIntegrationDecisionDefinition
    reason: string
  },
): Promise<InventoryAccountingIntegrationChangeRequestDetailResponse> {
  return clinicianPost(
    sessionId,
    '/api/inventory/accounting-integration-change-requests',
    input,
  )
}
export async function getInventoryAccountingIntegrationChangeRequest(
  sessionId: string,
  requestId: string,
): Promise<InventoryAccountingIntegrationChangeRequestDetailResponse> {
  return clinicianGet(
    sessionId,
    `/api/inventory/accounting-integration-change-requests/${requestId}`,
  )
}
export async function transitionInventoryAccountingIntegrationChangeRequest(
  sessionId: string,
  requestId: string,
  action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel',
  input: { expectedVersion: number; note?: string },
): Promise<InventoryAccountingIntegrationChangeRequestDetailResponse> {
  return clinicianPost(
    sessionId,
    `/api/inventory/accounting-integration-change-requests/${requestId}/${action}`,
    input,
  )
}
export type InventoryReplenishmentPolicyDefinition = {
  itemId: number
  facilityId: number
  reorderPoint: number
  targetQuantity: number
  leadTimeDays: number
  safetyStock: number
  preferredVendorId: string | null
  packSize: number
  approvalThreshold: number
  effectiveDate: string
  approvalReference: string
  rationale: string
}
export type InventoryReplenishmentPolicy = {
  policyId: string
  definition: InventoryReplenishmentPolicyDefinition
  revision: number
  status: 'active' | 'superseded'
  activatedAt: string
  activatedBy: string
  supersededAt: string | null
  supersededBy: string | null
}
export type InventoryReplenishmentPolicyChangeRequest = {
  requestId: string
  proposedDefinition: InventoryReplenishmentPolicyDefinition
  baselinePolicyId: string | null
  baselineRevision: number | null
  reason: string
  status:
    | 'draft'
    | 'submitted'
    | 'approved'
    | 'rejected'
    | 'activated'
    | 'cancelled'
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type InventoryReplenishmentPolicyCatalogResponse = {
  activePolicies: InventoryReplenishmentPolicy[]
  requests: InventoryReplenishmentPolicyChangeRequest[]
}
export type InventoryReplenishmentPolicyChangeRequestEvent = {
  eventId: number
  action: string
  note: string | null
  occurredAt: string
  username: string
}
export type InventoryReplenishmentPolicyChangeRequestDetailResponse = {
  request: InventoryReplenishmentPolicyChangeRequest
  activePolicy: InventoryReplenishmentPolicy | null
  events: InventoryReplenishmentPolicyChangeRequestEvent[]
}
export async function getInventoryReplenishmentPolicies(
  sessionId: string,
  signal?: AbortSignal,
): Promise<InventoryReplenishmentPolicyCatalogResponse> {
  return clinicianGet(sessionId, '/api/inventory/replenishment-policies', signal)
}
export async function createInventoryReplenishmentPolicyChangeRequest(
  sessionId: string,
  input: {
    proposedDefinition: InventoryReplenishmentPolicyDefinition
    reason: string
  },
): Promise<InventoryReplenishmentPolicyChangeRequestDetailResponse> {
  return clinicianPost(
    sessionId,
    '/api/inventory/replenishment-policy-change-requests',
    input,
  )
}
export async function getInventoryReplenishmentPolicyChangeRequest(
  sessionId: string,
  requestId: string,
): Promise<InventoryReplenishmentPolicyChangeRequestDetailResponse> {
  return clinicianGet(
    sessionId,
    `/api/inventory/replenishment-policy-change-requests/${requestId}`,
  )
}
export async function transitionInventoryReplenishmentPolicyChangeRequest(
  sessionId: string,
  requestId: string,
  action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel',
  input: { expectedVersion: number; note?: string },
): Promise<InventoryReplenishmentPolicyChangeRequestDetailResponse> {
  return clinicianPost(
    sessionId,
    `/api/inventory/replenishment-policy-change-requests/${requestId}/${action}`,
    input,
  )
}
export type InventoryReplenishmentRecommendation = {
  policyId: string
  policyRevision: number
  itemId: number
  itemCode: string
  itemName: string
  unit: string
  facilityId: number
  facilityCode: string
  facilityName: string
  onHand: number
  reorderPoint: number
  targetQuantity: number
  leadTimeDays: number
  safetyStock: number
  preferredVendorId: string | null
  preferredVendorName: string | null
  packSize: number
  approvalThreshold: number
  recommendedQuantity: number
  effectiveDate: string
  approvalReference: string
  canAutoOrder: boolean
}
export async function getInventoryReplenishmentRecommendations(
  sessionId: string,
  signal?: AbortSignal,
): Promise<InventoryReplenishmentRecommendation[]> {
  return clinicianGet(
    sessionId,
    '/api/inventory/replenishment-recommendations',
    signal,
  )
}
export type InventoryReceiptCostLayer = {
  layerId: string
  sourceTransactionId: string
  receiptId: string
  lotId: number
  itemId: number
  facilityId: number
  receivedQuantity: number
  remainingQuantity: number
  unitCost: number
  currency: string
  policyId: string | null
  policyRevision: number | null
  method: string | null
  status: 'open' | 'pending_policy' | 'exhausted' | 'corrected'
  createdAt: string
  createdBy: string
}
export type InventoryReceiptCostLayerApplication = {
  applicationId: string
  layerId: string
  sourceTransactionId: string
  applicationType: string
  quantity: number
  unitCost: number
  extendedCost: number
  roundingTrace: string
  reversalApplicationId: string | null
  appliedAt: string
  appliedBy: string
}
export async function getInventoryReceiptCostLayers(
  sessionId: string,
  input: { lotId?: number; limit?: number } = {},
): Promise<InventoryReceiptCostLayer[]> {
  const query = new URLSearchParams()
  if (input.lotId) query.set('lotId', String(input.lotId))
  if (input.limit) query.set('limit', String(input.limit))
  return clinicianGet(sessionId, `/api/inventory/receipt-cost-layers?${query}`)
}
export async function getInventoryReceiptCostLayerApplications(
  sessionId: string,
  layerId: string,
): Promise<InventoryReceiptCostLayerApplication[]> {
  return clinicianGet(
    sessionId,
    `/api/inventory/receipt-cost-layers/${layerId}/applications`,
  )
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
  reconciliationId?: string | null
}
export type InventoryFacility = {
  facilityId: number
  code: string
  name: string
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
export async function getInventory(
  sessionId: string,
  signal?: AbortSignal,
): Promise<InventoryResponse> {
  return clinicianGet(sessionId, '/api/inventory/', signal)
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
export async function getInventoryLotMetadataHistory(
  sessionId: string,
  lotId: number,
  signal?: AbortSignal,
): Promise<InventoryLotMetadataAuditItem[]> {
  return clinicianGet(
    sessionId,
    `/api/inventory/lots/${lotId}/metadata-history`,
    signal,
  )
}
export type InventoryMutationResponse = {
  transaction: InventoryTransactionItem
  lot: InventoryLot
  itemQuantityOnHand: number
  belowReorderPoint: boolean
  counterpartyLot?: InventoryLot | null
  transferId?: string | null
}
export async function createInventoryTransaction(
  sessionId: string,
  input: {
    lotId: number
    transactionType: 'consumption' | 'adjustment'
    quantity: number
    reason: string
    costLayerId?: string
    reversalApplicationId?: string
  },
  signal?: AbortSignal,
): Promise<InventoryMutationResponse> {
  return clinicianPost(sessionId, '/api/inventory/transactions', input, signal)
}
export async function createInventoryTransfer(
  sessionId: string,
  input: {
    sourceLotId: number
    destinationFacilityId: number
    quantity: number
    reason: string
    costLayerId?: string
  },
  signal?: AbortSignal,
): Promise<InventoryMutationResponse> {
  return clinicianPost(sessionId, '/api/inventory/transfers', input, signal)
}
export type InventoryCountReconciliationCreateInput = {
  lotId: number
  countedQuantity: number
  notes: string
}
export type InventoryCountReconciliation = {
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
export async function createInventoryCountReconciliation(
  sessionId: string,
  input: InventoryCountReconciliationCreateInput,
  signal?: AbortSignal,
): Promise<InventoryCountReconciliation> {
  return clinicianPost(
    sessionId,
    '/api/inventory/count-reconciliations',
    input,
    signal,
  )
}
export type InventoryLotDestructionCreateInput = {
  destructionDate?: string | null
  method: string
  witness: string
  notes: string
}
export type InventoryLotDestruction = {
  destructionId: string
  lot: InventoryLot
  quantityAffected: number
  transaction: InventoryTransactionItem
  destructionDate: string
  method: string
  witness: string
  notes: string
  destroyedBy: string
  recordedAt: string
}
export async function createInventoryLotDestruction(
  sessionId: string,
  lotId: number,
  input: InventoryLotDestructionCreateInput,
  signal?: AbortSignal,
): Promise<InventoryLotDestruction> {
  return clinicianPost(
    sessionId,
    `/api/inventory/lots/${lotId}/destructions`,
    input,
    signal,
  )
}
export type InventoryExpiryDispositionCreateInput = {
  disposition: 'quarantine' | 'return' | 'destroy'
  notes: string
  method?: string | null
  witness?: string | null
}
export type InventoryExpiryDisposition = {
  dispositionId: string
  disposition: 'quarantine' | 'return' | 'destroy'
  lot: InventoryLot
  quantityAffected: number
  notes: string
  method?: string | null
  witness?: string | null
  disposedBy: string
  disposedAt: string
  transaction?: InventoryTransactionItem | null
  destructionId?: string | null
}
export async function createInventoryExpiryDisposition(
  sessionId: string,
  lotId: number,
  input: InventoryExpiryDispositionCreateInput,
  signal?: AbortSignal,
): Promise<InventoryExpiryDisposition> {
  return clinicianPost(
    sessionId,
    `/api/inventory/lots/${lotId}/expiry-dispositions`,
    input,
    signal,
  )
}
export type InventoryVendor = {
  vendorId: string
  name: string
  contactName?: string | null
  phone?: string | null
  email?: string | null
  active: boolean
}
export type InventoryVendorsResponse = {
  vendors: InventoryVendor[]
}
export async function getInventoryVendors(
  sessionId: string,
  signal?: AbortSignal,
): Promise<InventoryVendorsResponse> {
  return clinicianGet(sessionId, '/api/inventory/vendors', signal)
}
export type InventoryPurchaseRequisitionLineInput = {
  itemId: number
  quantity: number
}
export type InventoryPurchaseRequisitionCreateInput = {
  facilityId: number
  vendorId?: string | null
  notes?: string | null
  lines: InventoryPurchaseRequisitionLineInput[]
}
export type InventoryPurchaseRequisitionLine = {
  requisitionLineId: string
  itemId: number
  itemCode: string
  itemName: string
  requestedQuantity: number
  receivedQuantity: number
  outstandingQuantity: number
  unit: string
}
export type InventoryPurchaseRequisitionEvent = {
  eventId: string
  action: string
  note?: string | null
  actor: string
  occurredAt: string
}
export type InventoryPurchaseRequisition = {
  requisitionId: string
  facilityId: number
  facilityCode: string
  facilityName: string
  vendorId?: string | null
  vendorName?: string | null
  status: string
  notes?: string | null
  requestedBy: string
  requestedAt: string
  submittedBy?: string | null
  submittedAt?: string | null
  decidedBy?: string | null
  decidedAt?: string | null
  decisionNotes?: string | null
  receiptStatus: string
  lines: InventoryPurchaseRequisitionLine[]
  events: InventoryPurchaseRequisitionEvent[]
}
export async function getInventoryPurchaseRequisitions(
  sessionId: string,
  signal?: AbortSignal,
): Promise<InventoryPurchaseRequisition[]> {
  return clinicianGet(sessionId, '/api/inventory/purchase-requisitions', signal)
}
export async function createInventoryPurchaseRequisition(
  sessionId: string,
  input: InventoryPurchaseRequisitionCreateInput,
  signal?: AbortSignal,
): Promise<InventoryPurchaseRequisition> {
  return clinicianPost(
    sessionId,
    '/api/inventory/purchase-requisitions',
    input,
    signal,
  )
}
export async function submitInventoryPurchaseRequisition(
  sessionId: string,
  requisitionId: string,
  signal?: AbortSignal,
): Promise<InventoryPurchaseRequisition> {
  return clinicianPost(
    sessionId,
    `/api/inventory/purchase-requisitions/${encodeURIComponent(requisitionId)}/submit`,
    undefined,
    signal,
  )
}
export async function decideInventoryPurchaseRequisition(
  sessionId: string,
  requisitionId: string,
  decision: 'approve' | 'reject',
  notes?: string | null,
  signal?: AbortSignal,
): Promise<InventoryPurchaseRequisition> {
  return clinicianPost(
    sessionId,
    `/api/inventory/purchase-requisitions/${encodeURIComponent(requisitionId)}/decisions/${decision}`,
    { notes: notes?.trim() || null },
    signal,
  )
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
export type InventoryPurchaseReceipt = {
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
export async function createInventoryPurchaseReceipt(
  sessionId: string,
  input: InventoryPurchaseReceiptCreateInput,
  signal?: AbortSignal,
): Promise<InventoryPurchaseReceipt> {
  return clinicianPost(
    sessionId,
    '/api/inventory/purchase-receipts',
    input,
    signal,
  )
}
export type InventoryPatientSaleCreateInput = {
  lotId: number
  patientId: string
  encounter: number
  saleDate?: string | null
  quantity: number
  fee: number
  notes?: string | null
}
export type InventoryPatientSale = {
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
export async function createInventoryPatientSale(
  sessionId: string,
  input: InventoryPatientSaleCreateInput,
  signal?: AbortSignal,
): Promise<InventoryPatientSale> {
  return clinicianPost(sessionId, '/api/inventory/patient-sales', input, signal)
}
export type InventoryPatientSaleAllocationCreateInput = Omit<
  InventoryPatientSaleCreateInput,
  'lotId'
> & {
  itemId: number
}
export type InventoryPatientSaleAllocationLine = {
  saleId: string
  lotId: number
  lotNumber: string
  quantity: number
  fee: number
  transactionId: string
}
export type InventoryPatientSaleAllocation = {
  saleBatchId: string
  itemId: number
  patientId: string
  encounter: number
  saleDate: string
  quantity: number
  fee: number
  allocations: InventoryPatientSaleAllocationLine[]
}
export async function allocateInventoryPatientSale(
  sessionId: string,
  input: InventoryPatientSaleAllocationCreateInput,
  signal?: AbortSignal,
): Promise<InventoryPatientSaleAllocation> {
  return clinicianPost(
    sessionId,
    '/api/inventory/patient-sales/allocate',
    input,
    signal,
  )
}
export type InventoryPrescriptionDispenseInput = {
  prescriptionId: string
  quantity: number
  fee: number
  saleDate?: string | null
  notes?: string | null
}
export type InventoryPrescriptionDispense = {
  prescriptionId: string
  itemId: number
  patientId: string
  encounter: number
  rxNormCode: string
  sale: InventoryPatientSale
}
export async function dispenseInventoryPrescription(
  sessionId: string,
  input: InventoryPrescriptionDispenseInput,
  signal?: AbortSignal,
): Promise<InventoryPrescriptionDispense> {
  return clinicianPost(
    sessionId,
    '/api/inventory/prescription-dispensations',
    input,
    signal,
  )
}
export type InventoryActivityReport = {
  datasetId: string
  datasetVersion: string
  fromDate?: string | null
  toDate?: string | null
  facilityId?: number | null
  totalEntries: number
  entries: InventoryTransactionItem[]
}
export async function getInventoryActivityReport(
  sessionId: string,
  filters: { from?: string; to?: string; facilityId?: number },
): Promise<InventoryActivityReport> {
  const query = new URLSearchParams()
  if (filters.from) query.set('from', filters.from)
  if (filters.to) query.set('to', filters.to)
  if (filters.facilityId) query.set('facilityId', String(filters.facilityId))
  return clinicianGet(sessionId, `/api/inventory/activity?${query}`)
}
export async function downloadInventoryActivityCsv(
  sessionId: string,
  filters: { from?: string; to?: string; facilityId?: number },
): Promise<Blob> {
  const query = new URLSearchParams()
  if (filters.from) query.set('from', filters.from)
  if (filters.to) query.set('to', filters.to)
  if (filters.facilityId) query.set('facilityId', String(filters.facilityId))
  const response = await fetch(
    `${apiBaseUrl}/api/inventory/activity/export?${query}`,
    {
      headers: { 'X-AvenChart-Session': sessionId },
    },
  )
  if (!response.ok)
    throw new Error(`Inventory export failed with ${response.status}`)
  return response.blob()
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

export type EncounterSoapNoteTemplate = {
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

export type EncounterSoapNoteTemplateCatalog = {
  datasetId: string
  datasetVersion: string
  asOfDate: string
  templates: EncounterSoapNoteTemplate[]
}

export type EncounterDiagnosisCode = {
  code: string
  description?: string | null
  sources: string[]
  billingLineCount: number
  procedureOrderCount: number
  supportingBillingCodes: string[]
}

export type EncounterDocumentAttachment = {
  id: number
  categoryId: number
  categoryName: string
  name: string
  docDate: string
  uploadedAt: string
  revisionAt: string
  currentVersion: number
  versionLabel: string
  versionStatus: string
  notes?: string | null
  deleted: number
  reviewStatus: string
  reviewedBy?: string | null
  reviewedAt?: string | null
  contentPreview?: string | null
  previewKind: string
  canDownload: boolean
}

export type EncounterSignature = {
  id: number
  signerUsername: string
  signedAt: string
  isLock: boolean
  amendment?: string | null
  hash: string
  signatureHash: string
  encounterVersion?: number | null
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
  referralSource?: string | null
  externalId?: string | null
  posCode?: number | null
  billingNote?: string | null
  rowVersion: number
  vitals?: EncounterVitals | null
  soapNote?: EncounterSoapNote | null
  billingLineCount: number
  diagnosisCodes: EncounterDiagnosisCode[]
  documents: EncounterDocumentAttachment[]
  signatures: EncounterSignature[]
  amendmentHistory: EncounterAmendmentHistoryItem[]
}

export type EncounterSearchResponse = {
  totalMatches: number
  encounters: EncounterListItem[]
}

export async function searchEncounters(
  sessionId: string,
  params: {
    patientId?: string
    fromDate?: string
    limit?: number
    archived?: boolean
  },
  signal?: AbortSignal,
): Promise<EncounterSearchResponse> {
  const q = new URLSearchParams()
  if (params.patientId) q.set('patientId', params.patientId)
  if (params.fromDate) q.set('from', params.fromDate)
  if (params.limit) q.set('limit', String(params.limit))
  if (params.archived) q.set('archived', 'true')
  return clinicianGet(sessionId, `/api/encounters/?${q}`, signal)
}

export async function getEncounterDetail(
  sessionId: string,
  encounterId: number,
  signal?: AbortSignal,
  includeArchivedDocuments = false,
): Promise<EncounterDetail> {
  return clinicianGet(
    sessionId,
    `/api/encounters/${encounterId}${includeArchivedDocuments ? '?includeArchivedDocuments=true' : ''}`,
    signal,
  )
}

export type EncounterUpdateInput = {
  reason: string
  sensitivity?: string | null
  referralSource?: string | null
  externalId?: string | null
  posCode?: number | null
  billingNote?: string | null
  expectedVersion: number
}
export async function updateEncounter(
  sessionId: string,
  encounterId: number,
  body: EncounterUpdateInput,
  signal?: AbortSignal,
): Promise<EncounterDetail> {
  return clinicianPut(sessionId, `/api/encounters/${encounterId}`, body, signal)
}
export type EncounterAuditEvent = {
  eventId: string
  occurredAt: string
  username: string
  action: string
  changedFields: string[]
}
export type EncounterAuditHistory = {
  encounter: number
  eventCount: number
  events: EncounterAuditEvent[]
}
export async function getEncounterAuditHistory(
  sessionId: string,
  encounterId: number,
  signal?: AbortSignal,
): Promise<EncounterAuditHistory> {
  return clinicianGet(sessionId, `/api/encounters/${encounterId}/audit`, signal)
}
export type EncounterLayoutFormOption = {
  key: string
  title: string
  value: string
  isDefault: boolean
}
export type EncounterLayoutFormField = {
  key: string
  groupKey: string
  label: string
  fieldType: 'text' | 'date' | 'select' | 'textarea' | 'checkbox' | 'number'
  required: boolean
  maxLength: number
  defaultValue: string
  options: EncounterLayoutFormOption[]
}
export type EncounterLayoutFormGroup = {
  key: string
  title: string
  fields: EncounterLayoutFormField[]
}
export type EncounterLayoutFormRecord = {
  recordId: string
  revision: number
  savedAt: string
  savedBy: string
  values: Record<string, string>
}
export type EncounterLayoutForm = {
  encounter: number
  layoutKey: string
  title: string
  groups: EncounterLayoutFormGroup[]
  latestRecord?: EncounterLayoutFormRecord | null
}
export type EncounterLayoutFormCatalog = {
  encounter: number
  forms: { key: string; title: string }[]
}
export async function getEncounterLayoutForms(
  sessionId: string,
  encounterId: number,
): Promise<EncounterLayoutFormCatalog> {
  return clinicianGet(sessionId, `/api/encounters/${encounterId}/forms`)
}
export async function getEncounterLayoutForm(
  sessionId: string,
  encounterId: number,
  key: string,
): Promise<EncounterLayoutForm> {
  return clinicianGet(sessionId, `/api/encounters/${encounterId}/forms/${key}`)
}
export async function saveEncounterLayoutForm(
  sessionId: string,
  encounterId: number,
  key: string,
  values: Record<string, string>,
): Promise<EncounterLayoutForm> {
  return clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/forms/${key}`,
    { values },
  )
}
export type EncounterClinicalAlert = {
  key: string
  title: string
  severity: 'info' | 'warning' | 'critical'
  message: string
  reason: string
}
export type EncounterClinicalAlerts = {
  encounter: number
  alerts: EncounterClinicalAlert[]
}
export async function getEncounterClinicalAlerts(
  sessionId: string,
  encounterId: number,
): Promise<EncounterClinicalAlerts> {
  return clinicianGet(sessionId, `/api/encounters/${encounterId}/alerts`)
}
export type EncounterClinicalAlertAcknowledgement = {
  ruleKey: string
  title: string
  acknowledgedAt: string
  acknowledgedBy: string
  reopenedAt?: string | null
  reopenedBy?: string | null
}
export type EncounterClinicalAlertHistory = {
  encounter: number
  acknowledgements: EncounterClinicalAlertAcknowledgement[]
}
export async function getEncounterClinicalAlertHistory(
  sessionId: string,
  encounterId: number,
): Promise<EncounterClinicalAlertHistory> {
  return clinicianGet(
    sessionId,
    `/api/encounters/${encounterId}/alerts/history`,
  )
}
export async function acknowledgeEncounterClinicalAlert(
  sessionId: string,
  encounterId: number,
  key: string,
): Promise<EncounterClinicalAlerts> {
  return clinicianPost(
    sessionId,
    `/api/encounters/${encounterId}/alerts/${encodeURIComponent(key)}/acknowledge`,
    {},
  )
}
export async function reopenEncounterClinicalAlert(
  sessionId: string,
  encounterId: number,
  key: string,
): Promise<EncounterClinicalAlerts> {
  return clinicianPost(
    sessionId,
    `/api/encounters/${encounterId}/alerts/${encodeURIComponent(key)}/reopen`,
    {},
  )
}
export async function archiveEncounter(
  sessionId: string,
  encounterId: number,
  signal?: AbortSignal,
): Promise<void> {
  await clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/archive`,
    {},
    signal,
  )
}
export async function restoreEncounter(
  sessionId: string,
  encounterId: number,
  signal?: AbortSignal,
): Promise<void> {
  await clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/restore`,
    {},
    signal,
  )
}

export type EncounterDocumentMutationResponse = {
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
export async function createEncounterDocument(
  sessionId: string,
  encounterId: number,
  body: EncounterDocumentCreateInput,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  return clinicianPost(
    sessionId,
    `/api/encounters/${encounterId}/documents`,
    body,
    signal,
  )
}

export type EncounterDocumentMetadataInput = {
  categoryId: number
  name: string
  docDate: string
  notes?: string | null
}
export async function updateEncounterDocumentMetadata(
  sessionId: string,
  encounterId: number,
  documentId: number,
  body: EncounterDocumentMetadataInput,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/documents/${documentId}/metadata`,
    body,
    signal,
  )
}

export async function archiveEncounterDocument(
  sessionId: string,
  encounterId: number,
  documentId: number,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/documents/${documentId}/soft-delete`,
    {},
    signal,
  )
}

export async function restoreEncounterDocument(
  sessionId: string,
  encounterId: number,
  documentId: number,
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/documents/${documentId}/restore`,
    {},
    signal,
  )
}

export async function signEncounterDocument(
  sessionId: string,
  encounterId: number,
  documentId: number,
  body: { reviewStatus: string; reviewedBy: string },
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/documents/${documentId}/sign`,
    body,
    signal,
  )
}

export async function replaceEncounterDocumentContent(
  sessionId: string,
  encounterId: number,
  documentId: number,
  body: { fileName: string; content: string },
  signal?: AbortSignal,
): Promise<EncounterDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/documents/${documentId}/content`,
    body,
    signal,
  )
}

export type EncounterDocumentMoveResponse = {
  id: number
  sourceDetail: EncounterDetail
  targetDetail: EncounterDetail
}
export async function moveEncounterDocument(
  sessionId: string,
  encounterId: number,
  documentId: number,
  targetEncounter: number,
  signal?: AbortSignal,
): Promise<EncounterDocumentMoveResponse> {
  return clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/documents/${documentId}/move`,
    { targetEncounter },
    signal,
  )
}

// ── Clinical Lists ────────────────────────────────────────────────────────────

export type ProblemListItem = {
  id: string
  title: string
  diagnosis?: string | null
  date?: string | null
  endDate?: string | null
  comments?: string | null
  activity: number
}

export type AllergyListItem = {
  id: string
  title: string
  reaction?: string | null
  severity?: string | null
  date?: string | null
  endDate?: string | null
  comments?: string | null
  activity: number
  listOptionId?: string | null
}

export type MedicationListItem = {
  id: string
  title: string
  diagnosis?: string | null
  date?: string | null
  endDate?: string | null
  comments?: string | null
  activity: number
  lifecycleVersion: number
  lifecycleEventCount: number
}

export type MedicationLifecycleHistoryResponse = {
  medicationId: string
  currentVersion: number
  eventCount: number
  events: Array<{
    eventId: number
    action: 'created' | 'deactivated' | 'restored'
    previousActivity?: number | null
    currentActivity: number
    actor: string
    reason?: string | null
    expectedVersion: number
    resultingVersion: number
    occurredAt: string
  }>
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
  enteredInError: boolean
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
  version: string
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
  staffResponse?: string | null
  patientNote?: string | null
  body: string
}

export type PrescriptionRefillQueueCounts = {
  pending: number
  clarificationRequested: number
  approved: number
  denied: number
  completed: number
  total: number
}

export type PrescriptionRefillQueueItem = {
  messageId: number
  threadId: number
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  portalUsername: string
  prescriptionId: string
  drug: string
  dosage?: string | null
  quantity?: string | null
  route?: string | null
  currentRefills: number
  requestDate: string
  status: string
  patientNote?: string | null
  staffResponse?: string | null
  updatedAt: string
  updatedBy: string
}

export type PrescriptionRefillQueueResponse = {
  datasetId: string
  datasetVersion: string
  statusFilter: string
  patientFilter?: string | null
  totalMatches: number
  returnedCount: number
  counts: PrescriptionRefillQueueCounts
  requests: PrescriptionRefillQueueItem[]
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

export type ClinicalPharmacyDirectoryItem = {
  id: number
  name: string
  transmitMethod: number
  email?: string | null
  ncpdp?: number | null
  npi?: number | null
}

export type ClinicalPharmacyDirectoryResponse = {
  datasetId: string
  datasetVersion: string
  pharmacyCount: number
  pharmacies: ClinicalPharmacyDirectoryItem[]
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

export async function getClinicalLists(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<ClinicalListsResponse> {
  return clinicianGet(sessionId, `/api/clinical-lists/${patientId}`, signal)
}

export async function searchClinicalMedicationVocabulary(
  sessionId: string,
  query: string,
  signal?: AbortSignal,
): Promise<MedicationVocabularyItem[]> {
  const params = new URLSearchParams()
  if (query.trim()) params.set('query', query.trim())
  return clinicianGet(
    sessionId,
    `/api/clinical-lists/medication-vocabulary?${params}`,
    signal,
  )
}

export async function getClinicalPharmacyDirectory(
  sessionId: string,
  signal?: AbortSignal,
): Promise<ClinicalPharmacyDirectoryResponse> {
  return clinicianGet(sessionId, '/api/clinical-lists/pharmacies', signal)
}

export async function getPrescriptionRefillQueue(
  sessionId: string,
  options: {
    status?: string
    patient?: string
    limit?: number
    offset?: number
  } = {},
  signal?: AbortSignal,
): Promise<PrescriptionRefillQueueResponse> {
  const params = new URLSearchParams()
  if (options.status) params.set('status', options.status)
  if (options.patient?.trim()) params.set('patient', options.patient.trim())
  if (options.limit !== undefined) params.set('limit', String(options.limit))
  if (options.offset !== undefined) params.set('offset', String(options.offset))
  const suffix = params.size > 0 ? `?${params}` : ''
  return clinicianGet(
    sessionId,
    `/api/clinical-lists/prescription-refill-requests${suffix}`,
    signal,
  )
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
  assignmentVersion: number
}

export type PatientMessagesResponse = {
  patientId: string
  patientDisplayName: string
  portalEnabled: boolean
  messages: PatientMessageItem[]
}

export type PatientMessageMutationResponse = {
  id: string
  detail: PatientMessagesResponse
}

export type PatientMessageAssignmentEvent = {
  eventId: number
  action: 'assigned' | 'reassigned' | 'unassigned' | string
  previousAssignedTo?: string | null
  assignedTo?: string | null
  reason?: string | null
  actor: string
  occurredAt: string
  assignmentVersion: number
}

export type PatientMessageAssignmentHistoryResponse = {
  messageId: string
  currentVersion: number
  events: PatientMessageAssignmentEvent[]
}

export type PatientMessageForwardRequest = {
  assignedTo: string
  expectedVersion: number
  note?: string | null
}

export type PatientMessageCorrectionHistoryResponse = {
  messageId: string
  events: Array<{
    eventId: number
    correction: string
    reason: string
    actor: string
    occurredAt: string
  }>
}

export type PatientMessageRetentionHistoryResponse = {
  messageId: string
  events: Array<{
    eventId: number
    action: 'archived' | 'restored'
    reason: string
    actor: string
    occurredAt: string
  }>
}

export type PatientMessageEscalationHistoryResponse = {
  messageId: string
  events: Array<{
    eventId: number
    action: 'escalated' | 'resolved'
    reason: string
    actor: string
    occurredAt: string
  }>
}

export type StaffMessageAttachmentItem = {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  sha256: string
  uploadedBy: string
  uploadedAt: string
}

export type StaffMessageInboxCounts = {
  total: number
  unread: number
  assignedToMe: number
  unassigned: number
}

export type StaffMessageInboxItem = {
  id: string
  patientId: string
  pubpid: string
  patientDisplayName: string
  date?: string | null
  subject: string
  preview: string
  status: string
  assignedTo?: string | null
  priority: 'normal' | 'urgent' | string
  ageDays: number
  unread: boolean
  portalRelation?: string | null
  updatedAt?: string | null
}

export type StaffMessageInboxResponse = {
  datasetId: string
  datasetVersion: string
  total: number
  offset: number
  limit: number
  counts: StaffMessageInboxCounts
  items: StaffMessageInboxItem[]
}

export type StaffMessageInboxQuery = {
  status?: string
  assignment?: 'all' | 'mine' | 'unassigned'
  patient?: string
  subject?: string
  priority?: 'all' | 'normal' | 'urgent'
  owner?: string
  minimumAgeDays?: number
  maximumAgeDays?: number
  offset?: number
  limit?: number
}

export async function getStaffMessageInbox(
  sessionId: string,
  query: StaffMessageInboxQuery = {},
  signal?: AbortSignal,
): Promise<StaffMessageInboxResponse> {
  const params = new URLSearchParams()
  Object.entries(query).forEach(([key, value]) => {
    if (value === undefined || value === '' || value === 'all') return
    params.set(key, String(value))
  })
  const suffix = params.size > 0 ? `?${params}` : ''
  return clinicianGet(sessionId, `/api/messages/inbox${suffix}`, signal)
}

export async function getPatientMessageAssignees(
  sessionId: string,
  signal?: AbortSignal,
): Promise<ClinicalWorkflowAssigneesResponse> {
  return clinicianGet(sessionId, '/api/messages/assignees', signal)
}

export async function getPatientMessages(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
  includeArchived = false,
): Promise<PatientMessagesResponse> {
  return clinicianGet(
    sessionId,
    `/api/messages/${patientId}${includeArchived ? '?includeArchived=true' : ''}`,
    signal,
  )
}

export async function replyToPatientMessage(
  sessionId: string,
  messageId: string,
  body: { body: string; assignedTo: string },
  signal?: AbortSignal,
): Promise<PatientMessagesResponse> {
  const result = await clinicianPut<PatientMessageMutationResponse>(
    sessionId,
    `/api/messages/${messageId}/reply`,
    body,
    signal,
  )
  return result.detail
}

export async function updatePatientMessageStatus(
  sessionId: string,
  messageId: string,
  body: { status: string; body: string },
  signal?: AbortSignal,
): Promise<PatientMessagesResponse> {
  const result = await clinicianPut<PatientMessageMutationResponse>(
    sessionId,
    `/api/messages/${messageId}/status`,
    body,
    signal,
  )
  return result.detail
}

export async function updatePatientMessageAssignment(
  sessionId: string,
  messageId: string,
  input: {
    assignedTo?: string | null
    expectedVersion: number
    reason?: string | null
  },
  signal?: AbortSignal,
): Promise<PatientMessagesResponse> {
  const result = await clinicianPut<PatientMessageMutationResponse>(
    sessionId,
    `/api/messages/${messageId}/assignment`,
    input,
    signal,
  )
  return result.detail
}

export async function getPatientMessageAssignmentHistory(
  sessionId: string,
  messageId: string,
  signal?: AbortSignal,
): Promise<PatientMessageAssignmentHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/messages/${messageId}/assignment-history`,
    signal,
  )
}

export async function forwardPatientMessage(
  sessionId: string,
  messageId: string,
  input: PatientMessageForwardRequest,
  signal?: AbortSignal,
): Promise<PatientMessagesResponse> {
  const result = await clinicianPost<PatientMessageMutationResponse>(
    sessionId,
    `/api/messages/${messageId}/forward`,
    input,
    signal,
  )
  return result.detail
}

export async function getStaffMessageAttachments(
  sessionId: string,
  messageId: string,
): Promise<StaffMessageAttachmentItem[]> {
  return clinicianGet(sessionId, `/api/messages/${messageId}/attachments`)
}

export async function uploadStaffMessageAttachment(
  sessionId: string,
  messageId: string,
  input: { fileName: string; contentType: string; contentBase64: string },
): Promise<StaffMessageAttachmentItem> {
  return clinicianPost(
    sessionId,
    `/api/messages/${messageId}/attachments`,
    input,
  )
}

export async function downloadStaffMessageAttachment(
  sessionId: string,
  messageId: string,
  attachmentId: string,
): Promise<Blob> {
  const response = await fetch(
    `${apiBaseUrl}/api/messages/${messageId}/attachments/${attachmentId}`,
    { headers: { 'X-AvenChart-Session': sessionId } },
  )
  await requireSuccessfulResponse(
    response,
    `GET /api/messages/${messageId}/attachments/${attachmentId}`,
    'clinician',
  )
  return response.blob()
}

export async function getStaffMessageCorrectionHistory(
  sessionId: string,
  messageId: string,
): Promise<PatientMessageCorrectionHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/messages/${messageId}/correction-history`,
  )
}

export async function correctStaffMessage(
  sessionId: string,
  messageId: string,
  input: { correction: string; reason: string },
): Promise<PatientMessagesResponse> {
  const result = await clinicianPost<PatientMessageMutationResponse>(
    sessionId,
    `/api/messages/${messageId}/correct`,
    input,
  )
  return result.detail
}

export async function getStaffMessageRetentionHistory(
  sessionId: string,
  messageId: string,
): Promise<PatientMessageRetentionHistoryResponse> {
  return clinicianGet(sessionId, `/api/messages/${messageId}/retention-history`)
}

export async function getStaffMessageEscalationHistory(
  sessionId: string,
  messageId: string,
): Promise<PatientMessageEscalationHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/messages/${messageId}/escalation-history`,
  )
}

export async function setStaffMessageEscalation(
  sessionId: string,
  messageId: string,
  escalated: boolean,
  reason: string,
): Promise<PatientMessageEscalationHistoryResponse> {
  return clinicianPost(
    sessionId,
    `/api/messages/${messageId}/${escalated ? 'escalate' : 'resolve-escalation'}`,
    { reason },
  )
}

export async function archiveStaffMessage(
  sessionId: string,
  messageId: string,
  reason: string,
): Promise<PatientMessagesResponse> {
  const result = await clinicianPost<PatientMessageMutationResponse>(
    sessionId,
    `/api/messages/${messageId}/archive`,
    { reason },
  )
  return result.detail
}

export async function restoreStaffMessage(
  sessionId: string,
  messageId: string,
  reason: string,
): Promise<PatientMessagesResponse> {
  const result = await clinicianPost<PatientMessageMutationResponse>(
    sessionId,
    `/api/messages/${messageId}/restore`,
    { reason },
  )
  return result.detail
}

export type OfficeNoteItem = {
  id: string
  body: string
  author: string
  groupName?: string | null
  active: boolean
  createdAt: string
  updatedAt: string
}
export type OfficeNotesResponse = { notes: OfficeNoteItem[]; total: number }
export async function getOfficeNotes(
  sessionId: string,
  activity: 'active' | 'inactive' | 'all' = 'active',
): Promise<OfficeNotesResponse> {
  return clinicianGet(sessionId, `/api/office-notes/?activity=${activity}`)
}
export async function createOfficeNote(
  sessionId: string,
  body: string,
): Promise<OfficeNoteItem> {
  return clinicianPost(sessionId, '/api/office-notes/', { body })
}
export async function updateOfficeNote(
  sessionId: string,
  id: string,
  body: string,
): Promise<OfficeNoteItem> {
  return clinicianPut(sessionId, `/api/office-notes/${id}`, { body })
}
export async function setOfficeNoteActivity(
  sessionId: string,
  id: string,
  active: boolean,
): Promise<OfficeNoteItem> {
  return clinicianPut(sessionId, `/api/office-notes/${id}/activity`, {
    active,
  })
}
export async function deleteOfficeNote(
  sessionId: string,
  id: string,
): Promise<void> {
  await clinicianDelete(sessionId, `/api/office-notes/${id}`)
}
export type AddressBookEntry = {
  id: number
  isInternal: boolean
  username?: string | null
  organization: string
  firstName: string
  lastName: string
  specialty?: string | null
  npi?: string | null
  type: string
  phone?: string | null
  mobile?: string | null
  fax?: string | null
  email?: string | null
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  active: boolean
}
export async function getAddressBook(
  sessionId: string,
  q = '',
): Promise<{ entries: AddressBookEntry[]; total: number }> {
  return clinicianGet(
    sessionId,
    `/api/administration/address-book/?organization=${encodeURIComponent(q)}&lastName=${encodeURIComponent(q)}`,
  )
}
export async function saveAddressBookContact(
  sessionId: string,
  input: Omit<AddressBookEntry, 'id' | 'isInternal' | 'username'>,
  id?: number,
): Promise<AddressBookEntry> {
  return id
    ? clinicianPut(sessionId, `/api/administration/address-book/${id}`, input)
    : clinicianPost(sessionId, '/api/administration/address-book/', input)
}
export async function deleteAddressBookContact(
  sessionId: string,
  id: number,
): Promise<void> {
  await clinicianDelete(sessionId, `/api/administration/address-book/${id}`)
}
export type TrackAnythingItem = {
  id: number
  parentId?: number | null
  name: string
  description?: string | null
  position: number
  active: boolean
}
export async function getTrackAnything(
  sessionId: string,
): Promise<{ items: TrackAnythingItem[] }> {
  return clinicianGet(sessionId, '/api/administration/tracks/')
}
export async function saveTrackAnything(
  sessionId: string,
  input: Omit<TrackAnythingItem, 'id'>,
  id?: number,
): Promise<TrackAnythingItem> {
  return id
    ? clinicianPut(sessionId, `/api/administration/tracks/${id}`, input)
    : clinicianPost(sessionId, '/api/administration/tracks/', input)
}
export async function deleteTrackAnything(
  sessionId: string,
  id: number,
): Promise<void> {
  await clinicianDelete(sessionId, `/api/administration/tracks/${id}`)
}
export type EncounterTrackDefinition = {
  id: number
  name: string
  description?: string | null
  items: TrackAnythingItem[]
}
export type EncounterTrackRecord = {
  recordId: string
  encounter: number
  trackTypeId: number
  trackName: string
  createdAt: string
  createdBy: string
}
export type EncounterTrackReadingValue = {
  itemTypeId: number
  itemName: string
  value: string
}
export type EncounterTrackReading = {
  readingId: string
  recordedAt: string
  recordedBy: string
  updatedAt?: string | null
  updatedBy?: string | null
  values: EncounterTrackReadingValue[]
}
export type EncounterTrackCatalog = {
  encounter: number
  availableTracks: EncounterTrackDefinition[]
  records: EncounterTrackRecord[]
}
export type EncounterTrackRecordDetail = {
  record: EncounterTrackRecord
  items: TrackAnythingItem[]
  readings: EncounterTrackReading[]
}
export async function getEncounterTracks(
  sessionId: string,
  encounter: number,
): Promise<EncounterTrackCatalog> {
  return clinicianGet(sessionId, `/api/encounters/${encounter}/tracks`)
}
export async function createEncounterTrack(
  sessionId: string,
  encounter: number,
  trackTypeId: number,
): Promise<EncounterTrackRecord> {
  return clinicianPost(sessionId, `/api/encounters/${encounter}/tracks`, {
    trackTypeId,
  })
}
export async function getEncounterTrack(
  sessionId: string,
  encounter: number,
  recordId: string,
): Promise<EncounterTrackRecordDetail> {
  return clinicianGet(
    sessionId,
    `/api/encounters/${encounter}/tracks/${recordId}`,
  )
}
export async function addEncounterTrackReading(
  sessionId: string,
  encounter: number,
  recordId: string,
  input: {
    recordedAt?: string
    values: { itemTypeId: number; value: string }[]
  },
): Promise<EncounterTrackReading> {
  return clinicianPost(
    sessionId,
    `/api/encounters/${encounter}/tracks/${recordId}/readings`,
    input,
  )
}
export async function updateEncounterTrackReading(
  sessionId: string,
  encounter: number,
  recordId: string,
  readingId: string,
  input: {
    recordedAt: string
    values: { itemTypeId: number; value: string }[]
  },
): Promise<EncounterTrackReading> {
  return clinicianPut(
    sessionId,
    `/api/encounters/${encounter}/tracks/${recordId}/readings/${readingId}`,
    input,
  )
}
export type PatientTrackHistoryReading = EncounterTrackReading
export type PatientTrackHistoryEncounter = {
  recordId: string
  encounter: number
  encounterDate: string
  trackName: string
  readings: PatientTrackHistoryReading[]
}
export type PatientTrackHistoryTrack = {
  trackTypeId: number
  trackName: string
  encounters: PatientTrackHistoryEncounter[]
}
export type PatientTrackHistory = {
  patientId: string
  tracks: PatientTrackHistoryTrack[]
}
export async function getPatientTrackHistory(
  sessionId: string,
  patientId: string,
): Promise<PatientTrackHistory> {
  return clinicianGet(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/track-history`,
  )
}
export type PatientEducationResource = {
  key: string
  title: string
  searchTemplate: string
  active: boolean
}
export async function getPatientEducationResources(
  sessionId: string,
): Promise<{ resources: PatientEducationResource[] }> {
  return clinicianGet(sessionId, '/api/patient-education/resources')
}
export async function searchPatientEducation(
  sessionId: string,
  resourceKey: string,
  searchText: string,
): Promise<{ url: string }> {
  return clinicianPost(sessionId, '/api/patient-education/search', {
    resourceKey,
    searchText,
  })
}
export type RecallItem = {
  id: string
  patientId: string
  patientName: string
  recallDate: string
  reason: string
  providerId?: number | null
  facilityId?: number | null
  status: string
  createdAt: string
  closedAt?: string | null
  closedBy?: string | null
  closureReason?: string | null
}
export async function getRecalls(
  sessionId: string,
  includeClosed = false,
): Promise<RecallItem[]> {
  return clinicianGet(sessionId, `/api/recalls/?includeClosed=${includeClosed}`)
}
export async function createRecall(
  sessionId: string,
  input: {
    patientId: string
    recallDate: string
    reason: string
    providerId?: number | null
    facilityId?: number | null
  },
): Promise<RecallItem> {
  return clinicianPost(sessionId, '/api/recalls/', input)
}
export async function closeRecall(
  sessionId: string,
  id: string,
  input: { status: 'completed' | 'cancelled'; reason: string },
): Promise<RecallItem> {
  return clinicianPost(sessionId, `/api/recalls/${id}/close`, input)
}
export type RecallActivityItem = {
  id: string
  activityType: 'phone' | 'postcard' | 'label'
  note?: string | null
  recordedAt: string
}
export async function getRecallActivity(
  sessionId: string,
  id: string,
): Promise<RecallActivityItem[]> {
  return clinicianGet(sessionId, `/api/recalls/${id}/activity`)
}
export async function addRecallActivity(
  sessionId: string,
  id: string,
  input: { activityType: 'phone' | 'postcard' | 'label'; note?: string | null },
): Promise<RecallActivityItem> {
  return clinicianPost(sessionId, `/api/recalls/${id}/activity`, input)
}
export type BatchCommunicationFilter = {
  processType: 'csv' | 'email' | 'phone'
  gender?: 'any' | 'male' | 'female'
  requireConsent: boolean
  ageFrom?: number
  ageTo?: number
  appointmentStart?: string
  appointmentEnd?: string
  seenSince?: string
  seenBefore?: string
  sortBy?: 'zipCode' | 'lastName' | 'appointmentDate'
}
export type BatchCommunicationRecipient = {
  patientId: string
  displayName: string
  email?: string | null
  phoneHome?: string | null
  phoneCell?: string | null
  postalCode?: string | null
  nextAppointmentDate?: string | null
  lastAppointmentDate?: string | null
  lastVisitDate?: string | null
  renderedSubject?: string | null
  renderedBody?: string | null
}
export type BatchCommunicationCampaign = {
  id: string
  filter: BatchCommunicationFilter
  processType: string
  emailSender?: string | null
  emailSubject?: string | null
  emailBody?: string | null
  recipientCount: number
  createdAt: string
}
export type BatchCommunicationDetail = {
  campaign: BatchCommunicationCampaign
  recipients: BatchCommunicationRecipient[]
}
export async function previewBatchCommunication(
  sessionId: string,
  filter: BatchCommunicationFilter,
): Promise<{
  filter: BatchCommunicationFilter
  recipients: BatchCommunicationRecipient[]
}> {
  return clinicianPost(sessionId, '/api/batch-communication/preview', {
    filter,
  })
}
export async function createBatchCommunicationCampaign(
  sessionId: string,
  input: {
    filter: BatchCommunicationFilter
    emailSender?: string
    emailSubject?: string
    emailBody?: string
  },
): Promise<BatchCommunicationDetail> {
  return clinicianPost(sessionId, '/api/batch-communication/campaigns', input)
}
export async function getBatchCommunicationCampaigns(
  sessionId: string,
): Promise<BatchCommunicationCampaign[]> {
  return clinicianGet(sessionId, '/api/batch-communication/campaigns')
}
export async function downloadBatchCommunicationCampaign(
  sessionId: string,
  id: string,
): Promise<Blob> {
  const response = await fetch(
    `${apiBaseUrl}/api/batch-communication/campaigns/${id}/output`,
    {
      headers: { 'X-AvenChart-Session': sessionId },
    },
  )
  if (!response.ok)
    throw new Error(
      `Batch communication download failed with ${response.status}`,
    )
  return response.blob()
}
export type ChartTrackerEvent = {
  id: string
  location?: string | null
  userId?: number | null
  userName?: string | null
  recordedAt: string
}
export type ChartTrackerPatient = {
  patientId: string
  publicId: string
  displayName: string
  dateOfBirth: string
  current?: ChartTrackerEvent | null
}
export type ChartTrackerOptions = {
  locations: string[]
  users: { id: number; displayName: string }[]
}
export async function getChartTrackerOptions(
  sessionId: string,
): Promise<ChartTrackerOptions> {
  return clinicianGet(sessionId, '/api/chart-tracker/options')
}
export async function lookupChartTrackerPatient(
  sessionId: string,
  identifier: string,
): Promise<ChartTrackerPatient> {
  return clinicianGet(
    sessionId,
    `/api/chart-tracker/lookup/${encodeURIComponent(identifier)}`,
  )
}
export async function getChartTrackerHistory(
  sessionId: string,
  patientId: string,
): Promise<ChartTrackerEvent[]> {
  return clinicianGet(
    sessionId,
    `/api/chart-tracker/patients/${encodeURIComponent(patientId)}/history`,
  )
}
export async function recordChartTrackerEvent(
  sessionId: string,
  patientId: string,
  input: { location?: string; userId?: number },
): Promise<ChartTrackerEvent> {
  return clinicianPost(
    sessionId,
    `/api/chart-tracker/patients/${encodeURIComponent(patientId)}/events`,
    input,
  )
}
export type DocumentTemplateItem = {
  id: string
  name: string
  content: string
  active: boolean
  createdAt: string
  updatedAt: string
}

export type DocumentTemplateListResponse = {
  search: string
  includeInactive: boolean
  offset: number
  limit: number
  total: number
  activeCount: number
  retiredCount: number
  items: DocumentTemplateItem[]
}

export async function getDocumentTemplates(
  sessionId: string,
  params: {
    search?: string
    includeInactive?: boolean
    offset?: number
    limit?: number
  } = {},
  signal?: AbortSignal,
): Promise<DocumentTemplateListResponse> {
  const query = new URLSearchParams()
  if (params.search) query.set('search', params.search)
  query.set('includeInactive', String(params.includeInactive ?? true))
  query.set('offset', String(params.offset ?? 0))
  query.set('limit', String(params.limit ?? 10))
  return clinicianGet(
    sessionId,
    `/api/administration/document-templates/?${query}`,
    signal,
  )
}
export async function createDocumentTemplate(
  sessionId: string,
  input: { name: string; content: string; active: boolean },
): Promise<DocumentTemplateItem> {
  return clinicianPost(
    sessionId,
    '/api/administration/document-templates/',
    input,
  )
}
export async function updateDocumentTemplate(
  sessionId: string,
  id: string,
  input: { name: string; content: string; active: boolean },
): Promise<DocumentTemplateItem> {
  return clinicianPut(
    sessionId,
    `/api/administration/document-templates/${id}`,
    input,
  )
}
export async function renderDocumentTemplate(
  sessionId: string,
  id: string,
  patientId: string,
): Promise<{ content: string }> {
  return clinicianPost(
    sessionId,
    `/api/administration/document-templates/${id}/render`,
    { patientId },
  )
}
export type DocumentTemplateBinaryVersion = {
  id: string
  templateId: string
  version: number
  fileName: string
  mimetype: string
  sizeBytes: number
  sha256: string
  createdAt: string
}

export type DocumentTemplateEvent = {
  eventId: number
  templateId: string
  action:
    | 'created'
    | 'updated'
    | 'activated'
    | 'retired'
    | 'binary-version-uploaded'
    | 'patient-attachment-generated'
  summary: string
  binaryVersionId?: string | null
  patientDocumentId?: number | null
  patientId?: string | null
  occurredAt: string
  username: string
}

export type DocumentTemplateHistoryResponse = {
  template: DocumentTemplateItem
  eventCount: number
  returnedCount: number
  resultLimit: number
  events: DocumentTemplateEvent[]
}

export async function getDocumentTemplateHistory(
  sessionId: string,
  id: string,
  signal?: AbortSignal,
): Promise<DocumentTemplateHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/administration/document-templates/${id}/history`,
    signal,
  )
}

export async function getDocumentTemplateBinaryVersions(
  sessionId: string,
  id: string,
): Promise<DocumentTemplateBinaryVersion[]> {
  return clinicianGet(
    sessionId,
    `/api/administration/document-templates/${id}/binary-versions`,
  )
}
export async function uploadDocumentTemplateBinaryVersion(
  sessionId: string,
  id: string,
  input: { fileName: string; mimetype: string; contentBase64: string },
): Promise<DocumentTemplateBinaryVersion> {
  return clinicianPost(
    sessionId,
    `/api/administration/document-templates/${id}/binary-versions`,
    input,
  )
}
export async function generateDocumentTemplateAttachment(
  sessionId: string,
  id: string,
  input: {
    patientId: string
    categoryId: number
    encounter?: number | null
    docDate?: string | null
    binaryVersionId?: string | null
  },
): Promise<{ id: number }> {
  return clinicianPost(
    sessionId,
    `/api/administration/document-templates/${id}/generate-attachment`,
    input,
  )
}
export async function downloadDocumentTemplateBinaryVersion(
  sessionId: string,
  id: string,
  versionId: string,
): Promise<Blob> {
  const r = await fetch(
    `${apiBaseUrl}/api/administration/document-templates/${id}/binary-versions/${versionId}/download`,
    { headers: { 'X-AvenChart-Session': sessionId } },
  )
  if (!r.ok) throw new Error(`Template download failed with ${r.status}`)
  return r.blob()
}
export type DuplicateReviewItem = {
  targetPatientId: string
  sourcePatientId: string
  targetDisplayName: string
  sourceDisplayName: string
  dateOfBirth: string
  matchScore: number
  matchReasons: string[]
  status: string
}
export async function getDuplicateReviewQueue(
  sessionId: string,
): Promise<{ items: DuplicateReviewItem[] }> {
  return clinicianGet(sessionId, '/api/patients/duplicates/review-queue')
}
export async function setDuplicateReviewDisposition(
  sessionId: string,
  input: {
    targetPatientId: string
    sourcePatientId: string
    status: string
    note?: string
  },
): Promise<DuplicateReviewItem> {
  return clinicianPut(
    sessionId,
    '/api/patients/duplicates/review-disposition',
    input,
  )
}

// ── Documents ─────────────────────────────────────────────────────────────────

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
  notes?: string | null
  deleted: number
  archiveStateActor?: string | null
  archiveStateAt?: string | null
  archiveEventCount: number
  reviewStatus: string
  reviewedBy?: string | null
  reviewedAt?: string | null
  contentPreview?: string | null
  previewKind: string
  previewStatus: string
  canPreviewInline: boolean
  canDownload: boolean
}

export type PatientDocumentVersionItem = {
  version: number
  versionLabel: string
  versionStatus: string
  capturedAt: string
  revisionActor?: string | null
  revisionReason?: string | null
  revisionAt: string
  fileName?: string | null
  mimetype?: string | null
  sizeBytes?: number | null
  pages?: number | null
  hash?: string | null
  contentPreview: string
  canDownload: boolean
}

export type PatientDocumentVersionHistoryResponse = {
  datasetId: string
  datasetVersion: string
  documentId: number
  documentKey: string
  patientId: string
  legacyPid: number
  name: string
  currentVersion: number
  versionCount: number
  versions: PatientDocumentVersionItem[]
}

export type PatientDocumentsResponse = {
  datasetId: string
  datasetVersion: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientDisplayName: string
  count: number
  activeCount: number
  archivedCount: number
  includesArchived: boolean
  documents: PatientDocumentItem[]
}

export type PatientDocumentCategoryOption = {
  id: number
  name: string
}

export type PatientDocumentCategoryOptionsResponse = {
  datasetId: string
  datasetVersion: string
  maxFileSizeBytes: number
  categories: PatientDocumentCategoryOption[]
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

export type PatientDocumentMutationResponse = {
  id: number
  detail: PatientDocumentsResponse
}

export type PatientDocumentMetadataUpdateInput = {
  categoryId: number
  name: string
  docDate: string
  encounter?: number | null
  notes?: string | null
  reason: string
}

export type PatientDocumentContentReplaceInput = {
  fileName: string
  content: string
  reason: string
  expectedVersion: number
}

export type PatientDocumentReviewInput = {
  reviewStatus: 'pending' | 'approved' | 'denied'
  reason: string
  expectedReviewStatus: 'pending' | 'approved' | 'denied'
}

export type PatientDocumentReviewEvent = {
  eventId: string
  fromStatus: string
  toStatus: string
  action: string
  reason: string
  actor: string
  occurredAt: string
  documentVersion: number
  contentHash?: string | null
}

export type PatientDocumentReviewHistoryResponse = {
  datasetId: string
  datasetVersion: string
  documentId: number
  documentKey: string
  patientId: string
  legacyPid: number
  name: string
  currentStatus: string
  currentReviewer?: string | null
  currentReviewedAt?: string | null
  eventCount: number
  returnedCount: number
  resultLimit: number
  events: PatientDocumentReviewEvent[]
}

export type PatientDocumentArchiveInput = {
  reason: string
  expectedArchived: boolean
}

export type PatientDocumentArchiveEvent = {
  eventId: string
  action: string
  fromArchived: boolean
  toArchived: boolean
  reason: string
  actor: string
  occurredAt: string
  documentVersion: number
  reviewStatus: string
  contentHash?: string | null
}

export type PatientDocumentArchiveHistoryResponse = {
  datasetId: string
  datasetVersion: string
  documentId: number
  documentKey: string
  patientId: string
  legacyPid: number
  name: string
  currentArchived: boolean
  currentStateActor?: string | null
  currentStateAt?: string | null
  eventCount: number
  returnedCount: number
  resultLimit: number
  events: PatientDocumentArchiveEvent[]
}

export type PatientDocumentOcrQueueCounts = {
  active: number
  queued: number
  running: number
  failed: number
  highPriority: number
  completed: number
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
  taskVersion: number
  inferred: boolean
  ageHours: number
  lastUpdatedAt: string
  startedBy?: string | null
  startedAt?: string | null
  completedBy?: string | null
  completedAt?: string | null
  failedBy?: string | null
  failedAt?: string | null
  failureReason?: string | null
  extractedTextLength: number
  extractedTextPreview?: string | null
  documentVersion: number
  reviewStatus: string
  notes?: string | null
}

export type PatientDocumentOcrQueueResponse = {
  datasetId: string
  datasetVersion: string
  count: number
  totalCount: number
  returnedCount: number
  offset: number
  limit: number
  statusFilter: string
  counts: PatientDocumentOcrQueueCounts
  items: PatientDocumentOcrQueueItem[]
}

export type PatientDocumentOcrQueueFilters = {
  patientId?: string
  status?: 'active' | 'queued' | 'running' | 'failed' | 'completed' | 'all'
  priority?: 'High' | 'Standard'
  query?: string
  offset?: number
  limit?: number
}

export type PatientDocumentOcrVersionedReasonInput = {
  expectedTaskVersion: number
  reason: string
}

export type PatientDocumentOcrCompleteInput = {
  extractedText: string
  expectedTaskVersion?: number
  reason?: string
}

export type PatientDocumentOcrCorrectInput = {
  expectedTaskVersion: number
  extractedText: string
  reason: string
}

export type PatientDocumentOcrMutationResponse = {
  id: number
  taskVersion: number
  status: string
  ocrStatus: string
  queueStatus: string
  extractedTextLength: number
  failureReason?: string | null
  updatedBy: string
  updatedAt: string
}

export type PatientDocumentOcrCompleteResponse = {
  id: number
  ocrStatus: string
  completedBy: string
  completedAt: string
  taskVersion: number
  status: string
}

export type PatientDocumentOcrEvent = {
  eventId: string
  action: string
  fromStatus: string
  toStatus: string
  reason: string
  actor: string
  occurredAt: string
  taskVersion: number
  documentVersion: number
  reviewStatus: string
  fromExtractedTextLength: number
  toExtractedTextLength: number
  fromExtractedTextPreview?: string | null
  toExtractedTextPreview?: string | null
  fromExtractedTextHash?: string | null
  toExtractedTextHash?: string | null
  failureReason?: string | null
}

export type PatientDocumentOcrHistoryResponse = {
  datasetId: string
  datasetVersion: string
  documentId: number
  documentKey: string
  patientId: string
  legacyPid: number
  name: string
  currentTaskVersion: number
  currentStatus: string
  currentOcrStatus: string
  currentExtractedText?: string | null
  currentFailureReason?: string | null
  currentStartedBy?: string | null
  currentStartedAt?: string | null
  currentCompletedBy?: string | null
  currentCompletedAt?: string | null
  currentFailedBy?: string | null
  currentFailedAt?: string | null
  eventCount: number
  returnedCount: number
  resultLimit: number
  events: PatientDocumentOcrEvent[]
}

export type PatientDocumentRoutingQueueCounts = {
  active: number
  pending: number
  inProgress: number
  unassigned: number
  highPriority: number
  overdue: number
  completed: number
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
  taskVersion: number
  inferred: boolean
  assignedTo?: string | null
  assignedDisplayName?: string | null
  routedAt: string
  dueAt: string
  ageHours: number
  isOverdue: boolean
  completedBy?: string | null
  completedAt?: string | null
  completionNote?: string | null
  notes?: string | null
}

export type PatientDocumentRoutingQueueResponse = {
  datasetId: string
  datasetVersion: string
  count: number
  totalCount: number
  returnedCount: number
  offset: number
  limit: number
  statusFilter: string
  counts: PatientDocumentRoutingQueueCounts
  items: PatientDocumentRoutingQueueItem[]
}

export type PatientDocumentRoutingQueueFilters = {
  patientId?: string
  status?: 'active' | 'pending' | 'in_progress' | 'completed' | 'all'
  priority?: 'High' | 'Standard'
  assignedTo?: string
  minimumAgeHours?: number
  query?: string
  offset?: number
  limit?: number
}

export type PatientDocumentRoutingAssignee = {
  staffId?: number | null
  username: string
  displayName: string
  role: string
}

export type PatientDocumentRoutingAssigneesResponse = {
  datasetId: string
  datasetVersion: string
  count: number
  assignees: PatientDocumentRoutingAssignee[]
}

export type PatientDocumentRoutingMutationInput = {
  destination: string
  priority: 'High' | 'Standard'
  assignedTo?: string | null
  reason: string
  dueAt?: string | null
  expectedTaskVersion: number
}

export type PatientDocumentRoutingCompleteInput = {
  reason: string
  expectedTaskVersion: number
}

export type PatientDocumentRoutingMutationResponse = {
  documentId: number
  taskVersion: number
  status: string
  assignedTo?: string | null
  destination: string
  priority: string
  dueAt: string
}

export type PatientDocumentRoutingEvent = {
  eventId: string
  action: string
  fromStatus: string
  toStatus: string
  fromDestination?: string | null
  toDestination: string
  fromPriority?: string | null
  toPriority: string
  fromAssignedTo?: string | null
  toAssignedTo?: string | null
  reason: string
  actor: string
  occurredAt: string
  dueAt: string
  taskVersion: number
  documentVersion: number
  reviewStatus: string
  contentHash?: string | null
}

export type PatientDocumentRoutingHistoryResponse = {
  datasetId: string
  datasetVersion: string
  documentId: number
  documentKey: string
  patientId: string
  legacyPid: number
  name: string
  currentTaskVersion: number
  currentStatus: string
  currentAssignedTo?: string | null
  currentDestination?: string | null
  currentPriority?: string | null
  currentDueAt?: string | null
  eventCount: number
  returnedCount: number
  resultLimit: number
  events: PatientDocumentRoutingEvent[]
}

export type PatientDocumentBinaryContentReplaceInput = {
  fileName: string
  mimetype: string
  contentBase64: string
  reason: string
  expectedVersion: number
}

export type PatientDocumentMetadataHistoryItem = {
  eventId: string
  changedFields: string[]
  fromCategoryId: number
  fromCategoryName: string
  toCategoryId: number
  toCategoryName: string
  fromName: string
  toName: string
  fromDocDate: string
  toDocDate: string
  fromEncounter?: number | null
  toEncounter?: number | null
  fromNotes?: string | null
  toNotes?: string | null
  reason: string
  actor: string
  occurredAt: string
}

export type PatientDocumentMetadataHistoryResponse = {
  datasetId: string
  datasetVersion: string
  documentId: number
  documentKey: string
  patientId: string
  legacyPid: number
  currentCategoryId: number
  currentCategoryName: string
  currentName: string
  currentDocDate: string
  currentEncounter?: number | null
  currentNotes?: string | null
  eventCount: number
  returnedCount: number
  resultLimit: number
  events: PatientDocumentMetadataHistoryItem[]
}

export async function getPatientDocuments(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
  includeArchived = false,
): Promise<PatientDocumentsResponse> {
  return clinicianGet(
    sessionId,
    `/api/documents/${encodeURIComponent(patientId)}${
      includeArchived ? '?includeArchived=true' : ''
    }`,
    signal,
  )
}

export async function getPatientDocumentCategoryOptions(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientDocumentCategoryOptionsResponse> {
  return clinicianGet(sessionId, '/api/documents/category-options', signal)
}

export async function createPatientDocument(
  sessionId: string,
  input: PatientDocumentCreateInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPost(sessionId, '/api/documents', input, signal)
}

export async function createPatientBinaryDocument(
  sessionId: string,
  input: PatientDocumentBinaryCreateInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPost(sessionId, '/api/documents/binary', input, signal)
}

export async function createPatientScannerCapture(
  sessionId: string,
  input: PatientDocumentScannerCaptureInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPost(
    sessionId,
    '/api/documents/scanner-captures',
    input,
    signal,
  )
}

export async function createPatientExternalLinkDocument(
  sessionId: string,
  input: PatientDocumentExternalLinkCreateInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPost(sessionId, '/api/documents/external-link', input, signal)
}

export async function getPatientDocumentMetadataHistory(
  sessionId: string,
  documentId: number,
  signal?: AbortSignal,
): Promise<PatientDocumentMetadataHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/metadata-history`,
    signal,
  )
}

export async function updatePatientDocumentMetadata(
  sessionId: string,
  documentId: number,
  input: PatientDocumentMetadataUpdateInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/metadata`,
    input,
    signal,
  )
}

export async function getPatientDocumentVersionHistory(
  sessionId: string,
  documentId: number,
  signal?: AbortSignal,
): Promise<PatientDocumentVersionHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/versions`,
    signal,
  )
}

export async function getPatientDocumentReviewHistory(
  sessionId: string,
  documentId: number,
  signal?: AbortSignal,
): Promise<PatientDocumentReviewHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/review-history`,
    signal,
  )
}

export async function reviewPatientDocument(
  sessionId: string,
  documentId: number,
  input: PatientDocumentReviewInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/sign`,
    input,
    signal,
  )
}

export async function getPatientDocumentArchiveHistory(
  sessionId: string,
  documentId: number,
  signal?: AbortSignal,
): Promise<PatientDocumentArchiveHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/archive-history`,
    signal,
  )
}

export async function archivePatientDocument(
  sessionId: string,
  documentId: number,
  input: PatientDocumentArchiveInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/soft-delete`,
    input,
    signal,
  )
}

export async function restorePatientDocument(
  sessionId: string,
  documentId: number,
  input: PatientDocumentArchiveInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/restore`,
    input,
    signal,
  )
}

export async function getPatientDocumentOcrQueue(
  sessionId: string,
  filters: PatientDocumentOcrQueueFilters = {},
  signal?: AbortSignal,
): Promise<PatientDocumentOcrQueueResponse> {
  const params = new URLSearchParams()
  if (filters.patientId?.trim())
    params.set('patientId', filters.patientId.trim())
  if (filters.status) params.set('status', filters.status)
  if (filters.priority) params.set('priority', filters.priority)
  if (filters.query?.trim()) params.set('query', filters.query.trim())
  if (filters.offset !== undefined) params.set('offset', String(filters.offset))
  if (filters.limit !== undefined) params.set('limit', String(filters.limit))
  const query = params.toString()
  return clinicianGet(
    sessionId,
    `/api/documents/ocr-queue${query ? `?${query}` : ''}`,
    signal,
  )
}

export async function getPatientDocumentOcrHistory(
  sessionId: string,
  documentId: number,
  signal?: AbortSignal,
): Promise<PatientDocumentOcrHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/ocr-history`,
    signal,
  )
}

export async function startPatientDocumentOcr(
  sessionId: string,
  documentId: number,
  input: PatientDocumentOcrVersionedReasonInput,
  signal?: AbortSignal,
): Promise<PatientDocumentOcrMutationResponse> {
  return clinicianPost(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/ocr/start`,
    input,
    signal,
  )
}

export async function failPatientDocumentOcr(
  sessionId: string,
  documentId: number,
  input: PatientDocumentOcrVersionedReasonInput,
  signal?: AbortSignal,
): Promise<PatientDocumentOcrMutationResponse> {
  return clinicianPost(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/ocr/fail`,
    input,
    signal,
  )
}

export async function completePatientDocumentOcr(
  sessionId: string,
  documentId: number,
  input: PatientDocumentOcrCompleteInput,
  signal?: AbortSignal,
): Promise<PatientDocumentOcrCompleteResponse> {
  return clinicianPost(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/ocr/complete`,
    input,
    signal,
  )
}

export async function correctPatientDocumentOcr(
  sessionId: string,
  documentId: number,
  input: PatientDocumentOcrCorrectInput,
  signal?: AbortSignal,
): Promise<PatientDocumentOcrMutationResponse> {
  return clinicianPost(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/ocr/correct`,
    input,
    signal,
  )
}

export async function getPatientDocumentRoutingQueue(
  sessionId: string,
  filters: PatientDocumentRoutingQueueFilters = {},
  signal?: AbortSignal,
): Promise<PatientDocumentRoutingQueueResponse> {
  const params = new URLSearchParams()
  if (filters.patientId?.trim())
    params.set('patientId', filters.patientId.trim())
  if (filters.status) params.set('status', filters.status)
  if (filters.priority) params.set('priority', filters.priority)
  if (filters.assignedTo?.trim()) {
    params.set('assignedTo', filters.assignedTo.trim())
  }
  if (filters.minimumAgeHours !== undefined) {
    params.set('minimumAgeHours', String(filters.minimumAgeHours))
  }
  if (filters.query?.trim()) params.set('query', filters.query.trim())
  if (filters.offset !== undefined) params.set('offset', String(filters.offset))
  if (filters.limit !== undefined) params.set('limit', String(filters.limit))
  const query = params.toString()
  return clinicianGet(
    sessionId,
    `/api/documents/routing-queue${query ? `?${query}` : ''}`,
    signal,
  )
}

export async function getPatientDocumentRoutingAssignees(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientDocumentRoutingAssigneesResponse> {
  return clinicianGet(sessionId, '/api/documents/routing-assignees', signal)
}

export async function getPatientDocumentRoutingHistory(
  sessionId: string,
  documentId: number,
  signal?: AbortSignal,
): Promise<PatientDocumentRoutingHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/routing-history`,
    signal,
  )
}

export async function routePatientDocument(
  sessionId: string,
  documentId: number,
  input: PatientDocumentRoutingMutationInput,
  signal?: AbortSignal,
): Promise<PatientDocumentRoutingMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/routing`,
    input,
    signal,
  )
}

export async function completePatientDocumentRouting(
  sessionId: string,
  documentId: number,
  input: PatientDocumentRoutingCompleteInput,
  signal?: AbortSignal,
): Promise<PatientDocumentRoutingMutationResponse> {
  return clinicianPost(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/routing/complete`,
    input,
    signal,
  )
}

export async function replacePatientDocumentContent(
  sessionId: string,
  documentId: number,
  input: PatientDocumentContentReplaceInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/content`,
    input,
    signal,
  )
}

export async function replacePatientDocumentBinaryContent(
  sessionId: string,
  documentId: number,
  input: PatientDocumentBinaryContentReplaceInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}/content/binary`,
    input,
    signal,
  )
}

export async function deletePatientDocument(
  sessionId: string,
  documentId: number,
  signal?: AbortSignal,
): Promise<void> {
  return clinicianDelete(
    sessionId,
    `/api/documents/${encodeURIComponent(String(documentId))}`,
    signal,
  )
}

export type DownloadedFile = {
  blob: Blob
  fileName: string
  contentType: string
}

function getDownloadFileName(response: Response, fallbackName: string): string {
  const disposition = response.headers.get('content-disposition') ?? ''
  const encodedName = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1]
  if (encodedName) {
    try {
      return decodeURIComponent(encodedName.replace(/^["']|["']$/g, ''))
    } catch {
      // Fall back when a server returns malformed percent encoding.
    }
  }

  return (
    disposition.match(/filename="?([^";]+)"?/i)?.[1]?.trim() || fallbackName
  )
}

export async function downloadPatientDocument(
  sessionId: string,
  documentId: number,
  fallbackName: string,
  signal?: AbortSignal,
): Promise<DownloadedFile> {
  const response = await fetch(
    `${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/download`,
    {
      headers: { 'X-AvenChart-Session': sessionId },
      signal,
    },
  )
  await requireSuccessfulResponse(response, 'Document download', 'clinician')

  const contentType =
    response.headers.get('content-type') ?? 'application/octet-stream'
  if (
    contentType.includes('text/html') ||
    contentType.includes('application/xhtml+xml')
  ) {
    throw new ApiRequestError(
      'The document service returned a web page instead of the requested file.',
      response.status,
    )
  }

  return {
    blob: await response.blob(),
    fileName: getDownloadFileName(response, fallbackName),
    contentType,
  }
}

export async function downloadPatientDocumentVersion(
  sessionId: string,
  documentId: number,
  version: number,
  fallbackName: string,
  signal?: AbortSignal,
): Promise<DownloadedFile> {
  const response = await fetch(
    `${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/versions/${encodeURIComponent(String(version))}/download`,
    {
      headers: { 'X-AvenChart-Session': sessionId },
      signal,
    },
  )
  await requireSuccessfulResponse(
    response,
    'Document version download',
    'clinician',
  )

  const contentType =
    response.headers.get('content-type') ?? 'application/octet-stream'
  if (
    contentType.includes('text/html') ||
    contentType.includes('application/xhtml+xml')
  ) {
    throw new ApiRequestError(
      'The document service returned a web page instead of the requested version.',
      response.status,
    )
  }

  return {
    blob: await response.blob(),
    fileName: getDownloadFileName(response, fallbackName),
    contentType,
  }
}

// ── Procedures / Lab Queue ────────────────────────────────────────────────────

export type ProcedureLabProviderDirectoryResponse = {
  datasetId: string
  datasetVersion: string
  includeInactive: boolean
  totalProviders: number
  activeProviders: number
  inactiveProviders: number
  providers: Array<{
    id: number
    name: string
    labDirectorName?: string | null
    labDirectorType?: string | null
    npi?: string | null
    protocol?: string | null
    usage?: string | null
    direction?: string | null
    notes?: string | null
    active: boolean
    orderCount: number
    reportCount: number
    futureOrderCount: number
  }>
}

export type ProcedureLabProviderAddressBookResponse = {
  datasetId: string
  datasetVersion: string
  organizations: Array<{
    id: number
    organization: string
    type: string
    active: boolean
  }>
}

export function getProcedureLabProviders(
  sessionId: string,
  includeInactive = true,
  signal?: AbortSignal,
): Promise<ProcedureLabProviderDirectoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/procedures/lab-providers?includeInactive=${includeInactive}`,
    signal,
  )
}

export function getProcedureLabProviderAddressBook(
  sessionId: string,
  signal?: AbortSignal,
): Promise<ProcedureLabProviderAddressBookResponse> {
  return clinicianGet(
    sessionId,
    '/api/procedures/lab-provider-address-book',
    signal,
  )
}

export async function createProcedureLabProviderOrganization(
  sessionId: string,
  input: { organization: string; type?: string; active: boolean },
): Promise<ProcedureLabProviderAddressBookResponse> {
  const result = await clinicianPost<{
    id: number
    addressBook: ProcedureLabProviderAddressBookResponse
  }>(sessionId, '/api/procedures/lab-provider-address-book', input)
  return result.addressBook
}

export function deleteProcedureLabProviderOrganization(
  sessionId: string,
  organizationId: number,
): Promise<void> {
  return clinicianDelete(
    sessionId,
    `/api/procedures/lab-provider-address-book/${organizationId}`,
  )
}

export type ProcedureOrderCatalogItem = {
  id: number
  parentId?: number | null
  labId?: number | null
  labName?: string | null
  name: string
  code?: string | null
  itemType: 'grp' | 'ord'
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

export type ProcedureOrderCatalogInput = {
  parentId?: number | null
  labId?: number | null
  name: string
  code?: string | null
  itemType: 'grp' | 'ord'
  procedureTypeName?: string | null
  description?: string | null
  specimen?: string | null
  standardCode?: string | null
  sequence?: number | null
  active: boolean
}

export function getProcedureOrderCatalog(
  sessionId: string,
  signal?: AbortSignal,
): Promise<ProcedureOrderCatalogResponse> {
  return clinicianGet(sessionId, '/api/procedures/order-catalog', signal)
}

export async function createProcedureOrderCatalogItem(
  sessionId: string,
  input: ProcedureOrderCatalogInput,
): Promise<ProcedureOrderCatalogResponse> {
  const result = await clinicianPost<{
    id: number
    catalog: ProcedureOrderCatalogResponse
  }>(sessionId, '/api/procedures/order-catalog', input)
  return result.catalog
}

export async function updateProcedureOrderCatalogItem(
  sessionId: string,
  itemId: number,
  input: ProcedureOrderCatalogInput,
): Promise<ProcedureOrderCatalogResponse> {
  const result = await clinicianPut<{
    id: number
    catalog: ProcedureOrderCatalogResponse
  }>(sessionId, `/api/procedures/order-catalog/${itemId}`, input)
  return result.catalog
}

export function deleteProcedureOrderCatalogItem(
  sessionId: string,
  itemId: number,
): Promise<void> {
  return clinicianDelete(sessionId, `/api/procedures/order-catalog/${itemId}`)
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
  versionHistory: Array<{
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
  }>
}

export type ProcedureReportItem = {
  id: number
  dateCollected: string
  reportDate: string
  specimenId?: number | null
  specimenNumber?: string | null
  status?: string | null
  reviewStatus?: string | null
  reviewedBy?: string | null
  reviewedAt?: string | null
  reviewVersion: number
  reviewHistoryCount: number
  notes?: string | null
  results: ProcedureResultItem[]
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
  specimens: Array<{
    id: number
    specimenIdentifier?: string | null
    accessionIdentifier?: string | null
    specimenType?: string | null
    collectionMethod?: string | null
    specimenLocation?: string | null
    collectedDate: string
    specimenCondition?: string | null
    comments?: string | null
    lifecycleStatus: "collected" | "labeled" | "received" | "rejected" | "recollected"
    lifecycleVersion: number
    lifecycleHistoryCount: number
  }>
  reports: ProcedureReportItem[]
}

export type ProcedureResultsResponse = {
  patientId: string
  pubpid: string
  patientDisplayName: string
  counts: {
    orders: number
    reports: number
    results: number
    finalResults: number
  }
  orders: ProcedureOrderItem[]
}

export function getProcedureResults(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<ProcedureResultsResponse> {
  return clinicianGet(
    sessionId,
    `/api/procedures/${encodeURIComponent(patientId.trim())}`,
    signal,
  )
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

export async function createProcedureOrder(
  sessionId: string,
  input: ProcedureOrderCreateInput,
): Promise<ProcedureResultsResponse> {
  const result = await clinicianPost<{
    id: number
    detail: ProcedureResultsResponse
  }>(sessionId, '/api/procedures/orders', input)
  return result.detail
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

export async function createProcedureSpecimen(
  sessionId: string,
  input: ProcedureSpecimenCreateInput,
): Promise<ProcedureResultsResponse> {
  const result = await clinicianPost<{
    id: number
    detail: ProcedureResultsResponse
  }>(sessionId, '/api/procedures/specimens', input)
  return result.detail
}

export type ProcedureSpecimenLifecycleInput = {
  status: "labeled" | "received" | "rejected" | "recollected"
  expectedVersion: number
  reason: string
}

export async function transitionProcedureSpecimenLifecycle(
  sessionId: string,
  specimenId: number,
  input: ProcedureSpecimenLifecycleInput,
): Promise<ProcedureResultsResponse> {
  const result = await clinicianPut<{
    id: number
    detail: ProcedureResultsResponse
  }>(
    sessionId,
    `/api/procedures/specimens/${specimenId}/lifecycle`,
    input,
  )
  return result.detail
}

export type ProcedureSpecimenLifecycleHistoryResponse = {
  specimenId: number
  lifecycleVersion: number
  events: Array<{
    eventId: number
    action: string
    previousStatus?: string | null
    currentStatus: string
    actor: string
    reason: string
    expectedVersion: number
    resultingVersion: number
    occurredAt: string
  }>
}

export function getProcedureSpecimenLifecycleHistory(
  sessionId: string,
  specimenId: number,
  signal?: AbortSignal,
): Promise<ProcedureSpecimenLifecycleHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/procedures/specimens/${specimenId}/lifecycle-history`,
    signal,
  )
}

export type ProcedureReportCreateInput = {
  orderId: number
  specimenId: number
  dateCollected: string
  dateReport: string
  reportStatus: string
  reviewStatus: string
  notes: string
}

export async function createProcedureReport(
  sessionId: string,
  input: ProcedureReportCreateInput,
): Promise<ProcedureResultsResponse> {
  const result = await clinicianPost<{
    id: number
    detail: ProcedureResultsResponse
  }>(sessionId, '/api/procedures/reports', input)
  return result.detail
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

export type ProcedureResultUpdateInput = Omit<
  ProcedureResultCreateInput,
  'reportId' | 'facility' | 'comments'
>

export async function createProcedureResult(
  sessionId: string,
  input: ProcedureResultCreateInput,
): Promise<ProcedureResultsResponse> {
  const result = await clinicianPost<{
    id: number
    detail: ProcedureResultsResponse
  }>(sessionId, '/api/procedures/results', input)
  return result.detail
}

export async function updateProcedureResult(
  sessionId: string,
  resultId: number,
  input: ProcedureResultUpdateInput,
): Promise<ProcedureResultsResponse> {
  const result = await clinicianPut<{
    id: number
    detail: ProcedureResultsResponse
  }>(sessionId, `/api/procedures/results/${resultId}`, input)
  return result.detail
}

export type ProcedureReportQueueItem = {
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
  reviewVersion: number
  reviewHistoryCount: number
  specimenNumber?: string | null
  notes?: string | null
}

export type ProcedureReportQueueResponse = {
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
  reports: ProcedureReportQueueItem[]
}

export type ProcedureQueueFilters = {
  status?: string
  patientId?: string
  providerId?: number
  labId?: number
  fromDate?: string
  toDate?: string
  limit?: number
}

function procedureQueueParams(filters: ProcedureQueueFilters = {}) {
  const params = new URLSearchParams()
  Object.entries(filters).forEach(([key, value]) => {
    if (value === undefined || value === '') return
    params.set(key, String(value))
  })
  return params
}

export async function getProcedureReportQueue(
  sessionId: string,
  filters: ProcedureQueueFilters = {},
  signal?: AbortSignal,
): Promise<ProcedureReportQueueResponse> {
  const params = procedureQueueParams(filters)
  return clinicianGet(
    sessionId,
    `/api/procedures/report-review-queue?${params}`,
    signal,
  )
}

export type ProcedureOrderQueueItem = {
  orderId: number
  patientId: string
  pubpid: string
  patientDisplayName: string
  orderDate: string
  providerName?: string | null
  providerId?: number | null
  labName?: string | null
  labId?: number | null
  procedureCode?: string | null
  procedureName?: string | null
  procedureType?: string | null
  encounterId?: number | null
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

export async function getProcedureOrderQueue(
  sessionId: string,
  filters: ProcedureQueueFilters = {},
  signal?: AbortSignal,
): Promise<ProcedureOrderQueueResponse> {
  const params = procedureQueueParams(filters)
  return clinicianGet(
    sessionId,
    `/api/procedures/order-queue?${params}`,
    signal,
  )
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

export type SavedReportDefinition = {
  id: string
  name: string
  reportType: string
  schedule: string
  active: boolean
  createdBy: string
  createdAt: string
  lastRunAt?: string | null
  runCount: number
}
export async function getSavedReportDefinitions(
  sessionId: string,
  signal?: AbortSignal,
): Promise<{ definitions: SavedReportDefinition[] }> {
  return clinicianGet(sessionId, '/api/reports/definitions', signal)
}
export async function createSavedReportDefinition(
  sessionId: string,
  body: {
    name: string
    schedule: string
    active: boolean
    reportType?: string
  },
  signal?: AbortSignal,
): Promise<SavedReportDefinition> {
  return clinicianPost(sessionId, '/api/reports/definitions', body, signal)
}
export async function runSavedReportDefinition(
  sessionId: string,
  id: string,
  signal?: AbortSignal,
): Promise<unknown> {
  return clinicianPost(
    sessionId,
    `/api/reports/definitions/${id}/run`,
    {},
    signal,
  )
}

export type TherapyGroup = {
  id: string
  name: string
  status: string
  facilitatorId?: number | null
  description?: string | null
  capacity: number
  createdAt: string
}
export type TherapyGroupMember = {
  groupId: string
  patientId: string
  legacyPid: number
  displayName: string
  joinedAt: string
}
export type TherapyGroupSession = {
  id: string
  groupId: string
  startsAt: string
  durationMinutes: number
  topic?: string | null
  status: string
  createdAt: string
}
export type TherapyGroupSessionAttendance = {
  sessionId: string
  patientId: string
  legacyPid: number
  displayName: string
  status: 'unrecorded' | 'present' | 'absent' | 'excused'
  note?: string | null
  recordedAt?: string | null
}
export type TherapyGroupSessionAttendanceResponse = {
  sessionId: string
  attendance: TherapyGroupSessionAttendance[]
}
export type TherapyGroupSessionEncounter = {
  sessionId: string
  patientId: string
  legacyPid: number
  displayName: string
  encounter?: number | null
  status: string
}
export type TherapyGroupSessionEncounterResponse = {
  sessionId: string
  encounters: TherapyGroupSessionEncounter[]
}
export async function getTherapyGroups(
  sessionId: string,
): Promise<{ groups: TherapyGroup[] }> {
  return clinicianGet(sessionId, '/api/therapy-groups/')
}
export async function createTherapyGroup(
  sessionId: string,
  body: {
    name: string
    facilitatorId?: number | null
    description?: string | null
    capacity: number
  },
): Promise<TherapyGroup> {
  return clinicianPost(sessionId, '/api/therapy-groups/', body)
}
export async function getTherapyGroupMembers(
  sessionId: string,
  groupId: string,
): Promise<TherapyGroupMember[]> {
  return clinicianGet(sessionId, `/api/therapy-groups/${groupId}/members`)
}
export async function addTherapyGroupMember(
  sessionId: string,
  groupId: string,
  patientId: string,
): Promise<TherapyGroupMember> {
  return clinicianPost(sessionId, `/api/therapy-groups/${groupId}/members`, {
    patientId,
  })
}
export async function getTherapyGroupSessions(
  sessionId: string,
  groupId: string,
): Promise<TherapyGroupSession[]> {
  return clinicianGet(sessionId, `/api/therapy-groups/${groupId}/sessions`)
}
export async function createTherapyGroupSession(
  sessionId: string,
  groupId: string,
  body: { startsAt: string; durationMinutes: number; topic?: string },
): Promise<TherapyGroupSession> {
  return clinicianPost(
    sessionId,
    `/api/therapy-groups/${groupId}/sessions`,
    body,
  )
}
export async function updateTherapyGroupSessionStatus(
  sessionId: string,
  groupId: string,
  groupSessionId: string,
  status: 'completed' | 'cancelled',
): Promise<TherapyGroupSession> {
  return clinicianPut(
    sessionId,
    `/api/therapy-groups/${groupId}/sessions/${groupSessionId}/status`,
    { status },
  )
}
export async function getTherapyGroupSessionAttendance(
  sessionId: string,
  groupId: string,
  groupSessionId: string,
): Promise<TherapyGroupSessionAttendanceResponse> {
  return clinicianGet(
    sessionId,
    `/api/therapy-groups/${groupId}/sessions/${groupSessionId}/attendance`,
  )
}
export async function recordTherapyGroupSessionAttendance(
  sessionId: string,
  groupId: string,
  groupSessionId: string,
  patientId: string,
  body: { status: 'present' | 'absent' | 'excused'; note?: string | null },
): Promise<TherapyGroupSessionAttendance> {
  return clinicianPut(
    sessionId,
    `/api/therapy-groups/${groupId}/sessions/${groupSessionId}/attendance/${encodeURIComponent(patientId)}`,
    body,
  )
}
export async function getTherapyGroupSessionEncounters(
  sessionId: string,
  groupId: string,
  groupSessionId: string,
): Promise<TherapyGroupSessionEncounter[]> {
  return clinicianGet(
    sessionId,
    `/api/therapy-groups/${groupId}/sessions/${groupSessionId}/encounters`,
  )
}
export async function createTherapyGroupSessionEncounters(
  sessionId: string,
  groupId: string,
  groupSessionId: string,
): Promise<TherapyGroupSessionEncounterResponse> {
  return clinicianPost(
    sessionId,
    `/api/therapy-groups/${groupId}/sessions/${groupSessionId}/encounters`,
    {},
  )
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
  ledgerEntries: BillingLedgerEntry[]
  encounters: Array<{
    id: number
    encounter: number
    date: string
    reason?: string | null
    balanceAmount: number
  }>
}

export async function getPatientBilling(
  sessionId: string,
  patientId: string,
  signal?: AbortSignal,
): Promise<PatientBillingResponse> {
  return clinicianGet(
    sessionId,
    `/api/billing/${encodeURIComponent(patientId.trim())}`,
    signal,
  )
}

export type BillingPaymentMutationResponse = {
  id: string
  sessionId: number
  detail: PatientBillingResponse
}

type BillingPaymentCommonInput = {
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
}

export type BillingPatientPaymentInput = BillingPaymentCommonInput & { payAmount: number }
export type BillingPatientRefundInput = BillingPaymentCommonInput & { refundAmount: number }
export type BillingInsurancePaymentInput = BillingPaymentCommonInput & {
  payerId: number
  payerName: string
  payAmount: number
  adjustmentAmount: number
  reasonCode: string
  payerClaimNumber?: string | null
}
export type BillingInsuranceReversalInput = BillingPaymentCommonInput & {
  payerId: number
  payerName: string
  reversalAmount: number
  payerClaimNumber?: string | null
}
export type BillingAdjustmentReversalInput = BillingPaymentCommonInput & {
  payerId: number
  payerName: string
  adjustmentAmount: number
  payerClaimNumber?: string | null
}

async function postBillingPayment(
  sessionId: string,
  path: string,
  input: unknown,
): Promise<PatientBillingResponse> {
  const response = await clinicianPost<BillingPaymentMutationResponse>(sessionId, path, input)
  return response.detail
}

export function createBillingPatientPayment(sessionId: string, input: BillingPatientPaymentInput) {
  return postBillingPayment(sessionId, '/api/billing/payments/patient-payments', input)
}

export function createBillingPatientRefund(sessionId: string, input: BillingPatientRefundInput) {
  return postBillingPayment(sessionId, '/api/billing/payments/patient-refunds', input)
}

export function createBillingInsurancePayment(sessionId: string, input: BillingInsurancePaymentInput) {
  return postBillingPayment(sessionId, '/api/billing/payments/insurance-payments', input)
}

export function createBillingInsuranceReversal(sessionId: string, input: BillingInsuranceReversalInput) {
  return postBillingPayment(sessionId, '/api/billing/payments/insurance-reversals', input)
}

export function createBillingAdjustmentReversal(sessionId: string, input: BillingAdjustmentReversalInput) {
  return postBillingPayment(sessionId, '/api/billing/payments/adjustment-reversals', input)
}

export async function importBillingEobBatch(sessionId: string, patientId: string): Promise<PatientBillingResponse> {
  const response = await clinicianPost<{ detail: PatientBillingResponse }>(
    sessionId,
    '/api/billing/eob-batches/import',
    { patientId },
  )
  return response.detail
}

export type StatementBatchCandidate = {
  patientId: string
  pubpid: string
  patientDisplayName: string
  statementNumber: string
  statementStatus: string
  statementDate: string
  dueDate: string
  balanceDueAmount: number
  pastDueAmount: number
  currentDueAmount: number
  oldestOpenAgeDays: number
  deliveryMethod: string
}

export type StatementBatchResponse = {
  asOfDate: string
  candidateCount: number
  totalBalanceAmount: number
  totalPastDueAmount: number
  totalCurrentDueAmount: number
  candidates: StatementBatchCandidate[]
}

export type StatementBatchDispatchResponse = {
  dispatchId: string
  dispatchedAt: string
  dispatchedStatementCount: number
  emailQueueCount: number
  printQueueCount: number
  totalBalanceAmount: number
}

export async function getBillingStatementBatch(
  sessionId: string,
  limit = 10,
  signal?: AbortSignal,
): Promise<StatementBatchResponse> {
  return clinicianGet(
    sessionId,
    `/api/billing/statements/batch?limit=${encodeURIComponent(String(limit))}`,
    signal,
  )
}

export async function dispatchBillingStatementBatch(
  sessionId: string,
  limit = 10,
  signal?: AbortSignal,
): Promise<StatementBatchDispatchResponse> {
  return clinicianPost(
    sessionId,
    `/api/billing/statements/batch/dispatch?limit=${encodeURIComponent(String(limit))}`,
    {},
    signal,
  )
}

export type CollectionsWorkQueueItem = {
  patientId: string
  pubpid: string
  patientDisplayName: string
  statementNumber: string
  balanceDueAmount: number
  pastDueAmount: number
  over90Amount: number
  oldestOpenAgeDays: number
  collectionTier: string
  recommendedAction: string
  contactMethod: string
}
export type CollectionsWorkQueueResponse = {
  asOfDate: string
  accountCount: number
  highPriorityCount: number
  totalBalanceAmount: number
  totalPastDueAmount: number
  totalOver90Amount: number
  items: CollectionsWorkQueueItem[]
}
export async function getBillingCollectionsWorkQueue(
  sessionId: string,
  limit = 10,
  signal?: AbortSignal,
): Promise<CollectionsWorkQueueResponse> {
  return clinicianGet(
    sessionId,
    `/api/billing/collections/work-queue?limit=${encodeURIComponent(String(limit))}`,
    signal,
  )
}
export async function createBillingCollectionsFollowUp(
  sessionId: string,
  body: {
    patientId: string
    assignedTo?: string | null
    action?: string | null
    note?: string | null
  },
  signal?: AbortSignal,
): Promise<unknown> {
  return clinicianPost(
    sessionId,
    '/api/billing/collections/follow-ups',
    body,
    signal,
  )
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
  facilityId?: number | null
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

export type AdministrationDirectoryCounts = {
  users: number
  providers: number
  facilities: number
  accessGroups: number
  accessGroupPermissions: number
  accessUserMemberships: number
  waitingPortalAudits: number
  waitingProfileReviews: number
}

export type AdministrationPortalProfileReviewRequest = {
  id: string
  requestedAt: string
  patientId: string
  legacyPid: number
  pubpid: string
  patientName: string
  activity: string
  pendingAction: string
  status: string
  narrative: string
  requestedDemographics: {
    email?: string | null
    phoneHome?: string | null
    phoneCell?: string | null
    street?: string | null
    city?: string | null
    state?: string | null
    postalCode?: string | null
  }
}

export type AdministrationDirectoryResponse = {
  counts: AdministrationDirectoryCounts
  users: AdministrationUserItem[]
  facilities: AdministrationFacilityItem[]
  accessControl: {
    groups: AdministrationAccessGroupItem[]
    permissions: AdministrationAccessPermissionItem[]
    groupPermissions: AdministrationAccessGroupPermissionItem[]
    userMemberships: AdministrationAccessUserMembershipItem[]
  }
  portalActivity: {
    waitingAuditCount: number
    waitingProfileReviewCount: number
    profileReviewRequests: AdministrationPortalProfileReviewRequest[]
  }
}

export async function getAdministrationDirectory(
  sessionId: string,
  signal?: AbortSignal,
): Promise<AdministrationDirectoryResponse> {
  return clinicianGet(sessionId, '/api/administration/directory', signal)
}
export type AuthorizationPolicyGap =
  | 'all'
  | 'production-approval'
  | 'facility-scope'
  | 'patient-scope'
  | 'purpose'
  | 'exceptional-access'
export type AuthorizationPolicyRule = {
  policyId: string
  capability: string
  permissionName: string
  section: string
  permission: string
  minimumLevel: string
  owner: string
  policyState: string
  approvalState: string
  subjectType: string
  organizationScope: string
  facilityScope: string
  patientScope: string
  purposeRequirement: string
  exceptionalAccess: string
  enforcement: string
  verificationState: string
  openGaps: string[]
}
export type AuthorizationPolicyCatalogResponse = {
  revision: string
  classification: string
  effectiveState: string
  rules: AuthorizationPolicyRule[]
  total: number
  returned: number
  offset: number
  limit: number
  query: string
  gap: AuthorizationPolicyGap
  counts: {
    total: number
    locallyEnforced: number
    productionApproved: number
    facilityScoped: number
    patientScoped: number
    purposeConditioned: number
    exceptionalAccessDecided: number
  }
  registryGaps: string[]
}
export async function getAuthorizationPolicyCatalog(
  sessionId: string,
  params: {
    query?: string
    gap?: AuthorizationPolicyGap
    offset?: number
    limit?: number
  } = {},
  signal?: AbortSignal,
): Promise<AuthorizationPolicyCatalogResponse> {
  const query = new URLSearchParams()
  if (params.query) query.set('query', params.query)
  if (params.gap) query.set('gap', params.gap)
  if (params.offset !== undefined) query.set('offset', String(params.offset))
  if (params.limit !== undefined) query.set('limit', String(params.limit))
  return clinicianGet(
    sessionId,
    `/api/administration/authorization-policy-catalog${
      query.size > 0 ? `?${query.toString()}` : ''
    }`,
    signal,
  )
}
export type ConfigurationCatalogItem = {
  key: string
  family: string
  classification: string
  authority: string
  validation: string
  mutationState: string
}
export async function getConfigurationCatalog(
  sessionId: string,
): Promise<{ settings: ConfigurationCatalogItem[] }> {
  return clinicianGet(sessionId, '/api/administration/configuration-catalog')
}
export type PracticeSettingItem = {
  key: string
  label: string
  value: string
  valueType: string
  updatedAt: string
  updatedBy: string
}
export async function getPracticeSettings(
  sessionId: string,
): Promise<{ settings: PracticeSettingItem[] }> {
  return clinicianGet(sessionId, '/api/administration/practice-settings')
}
export type PracticeSettingRegistryItem = {
  key: string
  label: string
  sourceOfTruth: string
  sensitivity: string
  allowedScopes: string[]
  owner: string
  requiredApprover: string
  rollbackPath: string
  impactClass: string
  dualControlRequired: boolean
  changeWindowRequired: boolean
  breakGlassPermitted: boolean
  impactPreviewAvailability: string
}
export async function getPracticeSettingRegistry(
  sessionId: string,
): Promise<{ registryRevision: string; items: PracticeSettingRegistryItem[] }> {
  return clinicianGet(
    sessionId,
    '/api/administration/practice-settings/registry',
  )
}
export type ConfigurationPackagePracticeSetting = {
  key: string
  value: string
  valueType: string
}
export type ConfigurationPackageDocument = {
  schema: string
  version: string
  practiceSettings: ConfigurationPackagePracticeSetting[]
}
export type ConfigurationPackageExport = {
  package: ConfigurationPackageDocument
  sha256: string
  exportedAt: string
  boundary: string
}
export type ConfigurationPackageDryRun = {
  sha256: string | null
  valid: boolean
  applyAvailable: boolean
  issues: Array<{ code: string; message: string }>
  conflicts: Array<{
    key: string
    currentValue: string
    proposedValue: string
    state: 'unchanged' | 'would-change'
  }>
  boundary: string
}
export async function exportConfigurationPackage(
  sessionId: string,
): Promise<ConfigurationPackageExport> {
  return clinicianPost(
    sessionId,
    '/api/administration/configuration-packages/export',
    {},
  )
}
export async function dryRunConfigurationPackage(
  sessionId: string,
  packageDocument: ConfigurationPackageDocument,
): Promise<ConfigurationPackageDryRun> {
  return clinicianPost(
    sessionId,
    '/api/administration/configuration-packages/dry-run',
    {
      package: packageDocument,
    },
  )
}
export type ConfigurationPackageImportRequest = {
  requestId: string
  sha256: string
  kind: 'import' | 'rollback'
  sourceRequestId: string | null
  reason: string
  status:
    'draft' | 'submitted' | 'approved' | 'rejected' | 'activated' | 'cancelled'
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type ConfigurationPackageImportRequestDetail = {
  request: ConfigurationPackageImportRequest
  currentConflicts: ConfigurationPackageDryRun['conflicts']
  events: Array<{
    eventId: number
    action: string
    note: string | null
    occurredAt: string
    username: string
  }>
}
export async function createConfigurationPackageImportRequest(
  sessionId: string,
  packageDocument: ConfigurationPackageDocument,
  reason: string,
): Promise<ConfigurationPackageImportRequestDetail> {
  return clinicianPost(
    sessionId,
    '/api/administration/configuration-package-import-requests',
    {
      package: packageDocument,
      reason,
    },
  )
}
export async function transitionConfigurationPackageImportRequest(
  sessionId: string,
  requestId: string,
  action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel',
  expectedVersion: number,
  note?: string,
): Promise<ConfigurationPackageImportRequestDetail> {
  return clinicianPost(
    sessionId,
    `/api/administration/configuration-package-import-requests/${requestId}/${action}`,
    {
      note: note ?? null,
      expectedVersion,
    },
  )
}
export async function getConfigurationPackageImportRequests(
  sessionId: string,
  input: {
    status?: string
    kind?: string
    offset?: number
    limit?: number
  } = {},
): Promise<{
  requests: ConfigurationPackageImportRequest[]
  total: number
  offset: number
  limit: number
}> {
  const query = new URLSearchParams()
  if (input.status) query.set('status', input.status)
  if (input.kind) query.set('kind', input.kind)
  if (input.offset !== undefined) query.set('offset', String(input.offset))
  if (input.limit !== undefined) query.set('limit', String(input.limit))
  return clinicianGet(
    sessionId,
    `/api/administration/configuration-package-import-requests?${query}`,
  )
}
export async function getConfigurationPackageImportRequest(
  sessionId: string,
  requestId: string,
): Promise<ConfigurationPackageImportRequestDetail> {
  return clinicianGet(
    sessionId,
    `/api/administration/configuration-package-import-requests/${requestId}`,
  )
}
export async function createConfigurationPackageCompensatingRollback(
  sessionId: string,
  requestId: string,
  reason: string,
): Promise<ConfigurationPackageImportRequestDetail> {
  return clinicianPost(
    sessionId,
    `/api/administration/configuration-package-import-requests/${requestId}/compensating-rollback`,
    { note: reason },
  )
}
export type PracticeSettingDelegation = {
  delegationId: string
  username: string
  settingKey: string
  facilityId: number
  expiresAt: string | null
  active: boolean
  reason: string
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export async function getPracticeSettingDelegations(
  sessionId: string,
): Promise<PracticeSettingDelegation[]> {
  return clinicianGet(
    sessionId,
    '/api/administration/practice-setting-delegations',
  )
}
export async function grantPracticeSettingDelegation(
  sessionId: string,
  input: {
    username: string
    settingKey: string
    facilityId: number
    expiresAt?: string | null
    reason: string
  },
): Promise<PracticeSettingDelegation> {
  return clinicianPost(
    sessionId,
    '/api/administration/practice-setting-delegations',
    input,
  )
}
export async function revokePracticeSettingDelegation(
  sessionId: string,
  delegationId: string,
  note: string,
): Promise<PracticeSettingDelegation> {
  return clinicianPost(
    sessionId,
    `/api/administration/practice-setting-delegations/${encodeURIComponent(delegationId)}/revoke`,
    { note },
  )
}
export type EffectivePracticeSettingItem = PracticeSettingItem & {
  sourceScope: 'system' | 'facility'
  sourceFacilityId: number | null
  facilityOverrideAvailable: boolean
}
export async function getEffectivePracticeSettings(
  sessionId: string,
  facilityId?: number,
): Promise<{
  requestedFacilityId: number | null
  settings: EffectivePracticeSettingItem[]
}> {
  return clinicianGet(
    sessionId,
    `/api/administration/practice-settings/effective${facilityId ? `?facilityId=${encodeURIComponent(facilityId)}` : ''}`,
  )
}
export async function updatePracticeSetting(
  sessionId: string,
  key: string,
  value: string,
): Promise<{ settings: PracticeSettingItem[] }> {
  return clinicianPut(
    sessionId,
    `/api/administration/practice-settings/${key}`,
    { value },
  )
}
export type PracticeSettingRevision = {
  revisionId: number
  value: string
  priorValue?: string | null
  action: string
  restoredFromRevisionId?: number | null
  occurredAt: string
  username: string
}
export type PracticeSettingHistory = {
  setting: PracticeSettingItem
  revisions: PracticeSettingRevision[]
}
export async function getPracticeSettingHistory(
  sessionId: string,
  key: string,
): Promise<PracticeSettingHistory> {
  return clinicianGet(
    sessionId,
    `/api/administration/practice-settings/${key}/history`,
  )
}
export async function rollbackPracticeSetting(
  sessionId: string,
  key: string,
  revisionId: number,
): Promise<PracticeSettingHistory> {
  return clinicianPost(
    sessionId,
    `/api/administration/practice-settings/${key}/revisions/${revisionId}/rollback`,
    {},
  )
}
export type PracticeSettingChangeRequestStatus =
  'draft' | 'submitted' | 'approved' | 'rejected' | 'activated' | 'cancelled'
export type PracticeSettingChangeRequestItem = {
  requestId: string
  settingKey: string
  facilityId: number | null
  proposedValue: string
  baselineValue: string
  baselineUpdatedAt: string
  reason: string
  status: PracticeSettingChangeRequestStatus
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type PracticeSettingChangeRequestCounts = Record<
  PracticeSettingChangeRequestStatus,
  number
>
export type PracticeSettingChangeRequestsResponse = {
  requests: PracticeSettingChangeRequestItem[]
  total: number
  returned: number
  offset: number
  limit: number
  status: 'all' | 'open' | PracticeSettingChangeRequestStatus
  settingKey?: string | null
  counts: PracticeSettingChangeRequestCounts
}
export type PracticeSettingChangeRequestEvent = {
  eventId: number
  action: string
  note?: string | null
  occurredAt: string
  username: string
}
export type PracticeSettingChangeRequestDetail = {
  request: PracticeSettingChangeRequestItem
  setting: PracticeSettingItem
  events: PracticeSettingChangeRequestEvent[]
}
export type PracticeSettingImpactPreviewItem = {
  resourceType: string
  previewAvailable: boolean
  affectedCount: number | null
  detail: string
}
export type PracticeSettingImpactPreview = {
  requestId: string
  settingKey: string
  scope: 'system' | 'facility'
  facilityId: number | null
  generatedAt: string
  impacts: PracticeSettingImpactPreviewItem[]
}
export type PracticeSettingChangeRequestAction =
  'submit' | 'approve' | 'reject' | 'activate' | 'cancel'

export async function getPracticeSettingChangeRequests(
  sessionId: string,
  params: {
    settingKey?: string
    status?: 'all' | 'open' | PracticeSettingChangeRequestStatus
    offset?: number
    limit?: number
  } = {},
  signal?: AbortSignal,
): Promise<PracticeSettingChangeRequestsResponse> {
  const query = new URLSearchParams()
  if (params.settingKey) query.set('settingKey', params.settingKey)
  if (params.status) query.set('status', params.status)
  if (params.offset !== undefined) query.set('offset', String(params.offset))
  if (params.limit !== undefined) query.set('limit', String(params.limit))
  return clinicianGet(
    sessionId,
    `/api/administration/practice-setting-change-requests${query.size > 0 ? `?${query.toString()}` : ''}`,
    signal,
  )
}

export async function getPracticeSettingChangeRequest(
  sessionId: string,
  requestId: string,
  signal?: AbortSignal,
): Promise<PracticeSettingChangeRequestDetail> {
  return clinicianGet(
    sessionId,
    `/api/administration/practice-setting-change-requests/${encodeURIComponent(requestId)}`,
    signal,
  )
}

export async function getPracticeSettingChangeRequestImpactPreview(
  sessionId: string,
  requestId: string,
  signal?: AbortSignal,
): Promise<PracticeSettingImpactPreview> {
  return clinicianGet(
    sessionId,
    `/api/administration/practice-setting-change-requests/${encodeURIComponent(requestId)}/impact-preview`,
    signal,
  )
}

export async function createPracticeSettingChangeRequest(
  sessionId: string,
  settingKey: string,
  input: { value: string; reason: string; facilityId?: number | null },
): Promise<PracticeSettingChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    `/api/administration/practice-settings/${encodeURIComponent(settingKey)}/change-requests`,
    input,
  )
}

export async function transitionPracticeSettingChangeRequest(
  sessionId: string,
  requestId: string,
  action: PracticeSettingChangeRequestAction,
  input: { note?: string | null; expectedVersion: number },
): Promise<PracticeSettingChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    `/api/administration/practice-setting-change-requests/${encodeURIComponent(requestId)}/${action}`,
    input,
  )
}

export async function deletePracticeSettingChangeRequestTestFixture(
  sessionId: string,
  requestId: string,
): Promise<void> {
  await clinicianDelete(
    sessionId,
    `/api/administration/practice-setting-change-requests/${encodeURIComponent(requestId)}/test-fixture`,
  )
}
export type CodingCatalogItem = {
  key: string
  displayName: string
  sequence: number
  active: boolean
  claimEnabled: boolean
  feeEnabled: boolean
  modifierLength: number
}
export async function getCodingCatalogs(
  sessionId: string,
): Promise<{ catalogs: CodingCatalogItem[] }> {
  return clinicianGet(sessionId, '/api/administration/coding-catalogs')
}
export type CodingCatalogMutationInput = {
  displayName: string
  sequence: number
  active: boolean
  claimEnabled: boolean
  feeEnabled: boolean
  modifierLength: number
}
export async function createCodingCatalog(
  sessionId: string,
  key: string,
  input: CodingCatalogMutationInput,
): Promise<{ catalogs: CodingCatalogItem[] }> {
  return clinicianPost(sessionId, '/api/administration/coding-catalogs', {
    key,
    ...input,
  })
}
export async function updateCodingCatalog(
  sessionId: string,
  key: string,
  input: CodingCatalogMutationInput,
): Promise<{ catalogs: CodingCatalogItem[] }> {
  return clinicianPut(
    sessionId,
    `/api/administration/coding-catalogs/${key}`,
    input,
  )
}
export type CodingCatalogRevision = {
  revisionId: number
  displayName: string
  sequence: number
  active: boolean
  claimEnabled: boolean
  feeEnabled: boolean
  modifierLength: number
  action: string
  restoredFromRevisionId?: number | null
  occurredAt: string
  username: string
}
export type CodingCatalogHistory = {
  catalog: CodingCatalogItem
  revisions: CodingCatalogRevision[]
}
export async function getCodingCatalogHistory(
  sessionId: string,
  key: string,
): Promise<CodingCatalogHistory> {
  return clinicianGet(
    sessionId,
    `/api/administration/coding-catalogs/${key}/history`,
  )
}
export async function rollbackCodingCatalog(
  sessionId: string,
  key: string,
  revisionId: number,
): Promise<CodingCatalogHistory> {
  return clinicianPost(
    sessionId,
    `/api/administration/coding-catalogs/${key}/revisions/${revisionId}/rollback`,
    {},
  )
}
export type CodingCatalogChangeRequestStatus =
  'draft' | 'submitted' | 'approved' | 'rejected' | 'activated' | 'cancelled'
export type CodingCatalogChangeRequestItem = {
  requestId: string
  catalogKey: string
  changeKind: 'create' | 'update'
  proposedDisplayName: string
  proposedSequence: number
  proposedActive: boolean
  proposedClaimEnabled: boolean
  proposedFeeEnabled: boolean
  proposedModifierLength: number
  baselineDisplayName?: string | null
  baselineSequence?: number | null
  baselineActive?: boolean | null
  baselineClaimEnabled?: boolean | null
  baselineFeeEnabled?: boolean | null
  baselineModifierLength?: number | null
  baselineUpdatedAt?: string | null
  reason: string
  status: CodingCatalogChangeRequestStatus
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type CodingCatalogChangeRequestEvent = {
  eventId: number
  action: string
  note?: string | null
  occurredAt: string
  username: string
}
export type CodingCatalogChangeRequestCounts = Record<
  CodingCatalogChangeRequestStatus,
  number
>
export type CodingCatalogChangeRequestsResponse = {
  requests: CodingCatalogChangeRequestItem[]
  total: number
  returned: number
  offset: number
  limit: number
  status: 'all' | 'open' | CodingCatalogChangeRequestStatus
  counts: CodingCatalogChangeRequestCounts
}
export type CodingCatalogChangeRequestDetail = {
  request: CodingCatalogChangeRequestItem
  activeCatalog?: CodingCatalogItem | null
  events: CodingCatalogChangeRequestEvent[]
}
export type CodingCatalogChangeRequestAction =
  'submit' | 'approve' | 'reject' | 'activate' | 'cancel'
export type CodingCatalogChangeRequestInput = {
  key: string
  displayName: string
  sequence: number
  active: boolean
  claimEnabled: boolean
  feeEnabled: boolean
  modifierLength: number
  reason: string
}
export async function getCodingCatalogChangeRequests(
  sessionId: string,
  params: {
    status?: 'all' | 'open' | CodingCatalogChangeRequestStatus
    offset?: number
    limit?: number
  } = {},
  signal?: AbortSignal,
): Promise<CodingCatalogChangeRequestsResponse> {
  const query = new URLSearchParams()
  if (params.status) query.set('status', params.status)
  if (params.offset !== undefined) query.set('offset', String(params.offset))
  if (params.limit !== undefined) query.set('limit', String(params.limit))
  return clinicianGet(
    sessionId,
    `/api/administration/coding-catalog-change-requests${query.size > 0 ? `?${query.toString()}` : ''}`,
    signal,
  )
}
export async function getCodingCatalogChangeRequest(
  sessionId: string,
  requestId: string,
): Promise<CodingCatalogChangeRequestDetail> {
  return clinicianGet(
    sessionId,
    `/api/administration/coding-catalog-change-requests/${encodeURIComponent(requestId)}`,
  )
}
export async function createCodingCatalogChangeRequest(
  sessionId: string,
  input: CodingCatalogChangeRequestInput,
): Promise<CodingCatalogChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    '/api/administration/coding-catalog-change-requests',
    input,
  )
}
export async function transitionCodingCatalogChangeRequest(
  sessionId: string,
  requestId: string,
  action: CodingCatalogChangeRequestAction,
  input: { note?: string | null; expectedVersion: number },
): Promise<CodingCatalogChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    `/api/administration/coding-catalog-change-requests/${encodeURIComponent(requestId)}/${action}`,
    input,
  )
}
export type FormLayoutItem = {
  key: string
  title: string
  mapping: string
  sequence: number
  active: boolean
}
export type FormLayoutGroupItem = {
  key: string
  title: string
  sequence: number
  active: boolean
}
export type FormLayoutFieldItem = {
  key: string
  groupKey: string
  label: string
  fieldType: string
  sequence: number
  required: boolean
  active: boolean
  maxLength: number
  listId: string
  defaultValue: string
}
export type FormLayoutDetail = {
  layout: FormLayoutItem
  groups: FormLayoutGroupItem[]
  fields: FormLayoutFieldItem[]
}
export async function getFormLayouts(
  sessionId: string,
): Promise<{ layouts: FormLayoutItem[] }> {
  return clinicianGet(sessionId, '/api/administration/form-layouts')
}
export async function getFormLayout(
  sessionId: string,
  key: string,
): Promise<FormLayoutDetail> {
  return clinicianGet(sessionId, `/api/administration/form-layouts/${key}`)
}
export type FormLayoutRevision = {
  revisionId: number
  title: string
  mapping: string
  sequence: number
  active: boolean
  groupCount: number
  fieldCount: number
  action: string
  restoredFromRevisionId?: number | null
  occurredAt: string
  username: string
}
export type FormLayoutHistory = {
  detail: FormLayoutDetail
  revisions: FormLayoutRevision[]
}
export async function getFormLayoutHistory(
  sessionId: string,
  key: string,
): Promise<FormLayoutHistory> {
  return clinicianGet(
    sessionId,
    `/api/administration/form-layouts/${key}/history`,
  )
}
export async function rollbackFormLayout(
  sessionId: string,
  key: string,
  revisionId: number,
): Promise<FormLayoutHistory> {
  return clinicianPost(
    sessionId,
    `/api/administration/form-layouts/${key}/revisions/${revisionId}/rollback`,
    {},
  )
}
export async function saveFormLayout(
  sessionId: string,
  key: string,
  input: Omit<FormLayoutItem, 'key'>,
): Promise<FormLayoutDetail> {
  return clinicianPut(
    sessionId,
    `/api/administration/form-layouts/${key}`,
    input,
  )
}
export async function saveFormLayoutGroup(
  sessionId: string,
  layoutKey: string,
  key: string,
  input: Omit<FormLayoutGroupItem, 'key'>,
): Promise<FormLayoutDetail> {
  return clinicianPut(
    sessionId,
    `/api/administration/form-layouts/${layoutKey}/groups/${key}`,
    input,
  )
}
export async function saveFormLayoutField(
  sessionId: string,
  layoutKey: string,
  key: string,
  input: Omit<FormLayoutFieldItem, 'key'>,
): Promise<FormLayoutDetail> {
  return clinicianPut(
    sessionId,
    `/api/administration/form-layouts/${layoutKey}/fields/${key}`,
    input,
  )
}
export type FormOptionListItem = {
  key: string
  title: string
  active: boolean
  optionCount: number
}
export type FormOptionValueItem = {
  key: string
  title: string
  sequence: number
  isDefault: boolean
  active: boolean
  value: string
}
export type FormOptionListDetail = {
  list: FormOptionListItem
  options: FormOptionValueItem[]
}
export async function getFormOptionLists(
  sessionId: string,
): Promise<{ lists: FormOptionListItem[] }> {
  return clinicianGet(sessionId, '/api/administration/form-option-lists')
}
export async function getFormOptionList(
  sessionId: string,
  key: string,
): Promise<FormOptionListDetail> {
  return clinicianGet(sessionId, `/api/administration/form-option-lists/${key}`)
}
export type FormOptionListRevision = {
  revisionId: number
  title: string
  active: boolean
  optionCount: number
  action: string
  restoredFromRevisionId?: number | null
  occurredAt: string
  username: string
}
export type FormOptionListHistory = {
  detail: FormOptionListDetail
  revisions: FormOptionListRevision[]
}
export async function getFormOptionListHistory(
  sessionId: string,
  key: string,
): Promise<FormOptionListHistory> {
  return clinicianGet(
    sessionId,
    `/api/administration/form-option-lists/${key}/history`,
  )
}
export async function rollbackFormOptionList(
  sessionId: string,
  key: string,
  revisionId: number,
): Promise<FormOptionListHistory> {
  return clinicianPost(
    sessionId,
    `/api/administration/form-option-lists/${key}/revisions/${revisionId}/rollback`,
    {},
  )
}
export async function saveFormOptionList(
  sessionId: string,
  key: string,
  input: Omit<FormOptionListItem, 'key' | 'optionCount'>,
): Promise<FormOptionListDetail> {
  return clinicianPut(
    sessionId,
    `/api/administration/form-option-lists/${key}`,
    input,
  )
}
export async function saveFormOptionValue(
  sessionId: string,
  listKey: string,
  key: string,
  input: Omit<FormOptionValueItem, 'key'>,
): Promise<FormOptionListDetail> {
  return clinicianPut(
    sessionId,
    `/api/administration/form-option-lists/${listKey}/options/${key}`,
    input,
  )
}
export type GovernanceStatus =
  'draft' | 'submitted' | 'approved' | 'rejected' | 'activated' | 'cancelled'
export type GovernanceEvent = {
  eventId: number
  action: GovernanceStatus | 'created'
  note?: string | null
  occurredAt: string
  username: string
}
export type FormLayoutDefinition = {
  key: string
  title: string
  mapping: string
  sequence: number
  active: boolean
  groups: Array<
    Pick<FormLayoutGroupItem, 'key' | 'title' | 'sequence' | 'active'>
  >
  fields: Array<
    Pick<
      FormLayoutFieldItem,
      | 'key'
      | 'groupKey'
      | 'label'
      | 'fieldType'
      | 'sequence'
      | 'required'
      | 'active'
      | 'maxLength'
      | 'listId'
      | 'defaultValue'
    >
  >
}
export type FormLayoutChangeRequest = {
  requestId: string
  layoutKey: string
  changeKind: 'create' | 'update'
  proposedDefinition: FormLayoutDefinition
  baselineDefinition?: FormLayoutDefinition | null
  baselineUpdatedAt?: string | null
  reason: string
  status: GovernanceStatus
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type FormLayoutChangeRequestDetail = {
  request: FormLayoutChangeRequest
  activeLayout?: FormLayoutDetail | null
  events: GovernanceEvent[]
}
export type FormChangeRequestStatus = 'open' | 'all' | GovernanceStatus
export type FormChangeRequestListOptions = {
  status?: FormChangeRequestStatus
  offset?: number
  limit?: number
}
export type FormLayoutChangeRequestList = {
  requests: FormLayoutChangeRequest[]
  total: number
  returned: number
  offset: number
  limit: number
  status: FormChangeRequestStatus
}
export async function getFormLayoutChangeRequests(
  sessionId: string,
  options: FormChangeRequestListOptions = {},
): Promise<FormLayoutChangeRequestList> {
  const parameters = new URLSearchParams({
    status: options.status ?? 'open',
    offset: String(options.offset ?? 0),
    limit: String(options.limit ?? 25),
  })
  return clinicianGet(
    sessionId,
    `/api/administration/form-layout-change-requests?${parameters.toString()}`,
  )
}
export async function getFormLayoutChangeRequest(
  sessionId: string,
  requestId: string,
): Promise<FormLayoutChangeRequestDetail> {
  return clinicianGet(
    sessionId,
    `/api/administration/form-layout-change-requests/${encodeURIComponent(requestId)}`,
  )
}
export async function createFormLayoutChangeRequest(
  sessionId: string,
  input: FormLayoutDefinition & { reason: string },
): Promise<FormLayoutChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    '/api/administration/form-layout-change-requests',
    input,
  )
}
export async function transitionFormLayoutChangeRequest(
  sessionId: string,
  requestId: string,
  action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel',
  input: { note?: string | null; expectedVersion: number },
): Promise<FormLayoutChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    `/api/administration/form-layout-change-requests/${encodeURIComponent(requestId)}/${action}`,
    input,
  )
}
export type FormOptionListDefinition = {
  key: string
  title: string
  active: boolean
  options: Array<
    Pick<
      FormOptionValueItem,
      'key' | 'title' | 'sequence' | 'isDefault' | 'active' | 'value'
    >
  >
}
export type FormOptionListChangeRequest = {
  requestId: string
  listKey: string
  changeKind: 'create' | 'update'
  proposedDefinition: FormOptionListDefinition
  baselineDefinition?: FormOptionListDefinition | null
  baselineUpdatedAt?: string | null
  reason: string
  status: GovernanceStatus
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type FormOptionListChangeRequestDetail = {
  request: FormOptionListChangeRequest
  activeList?: FormOptionListDetail | null
  events: GovernanceEvent[]
}
export type FormOptionListChangeRequestList = {
  requests: FormOptionListChangeRequest[]
  total: number
  returned: number
  offset: number
  limit: number
  status: FormChangeRequestStatus
}
export async function getFormOptionListChangeRequests(
  sessionId: string,
  options: FormChangeRequestListOptions = {},
): Promise<FormOptionListChangeRequestList> {
  const parameters = new URLSearchParams({
    status: options.status ?? 'open',
    offset: String(options.offset ?? 0),
    limit: String(options.limit ?? 25),
  })
  return clinicianGet(
    sessionId,
    `/api/administration/form-option-list-change-requests?${parameters.toString()}`,
  )
}
export async function getFormOptionListChangeRequest(
  sessionId: string,
  requestId: string,
): Promise<FormOptionListChangeRequestDetail> {
  return clinicianGet(
    sessionId,
    `/api/administration/form-option-list-change-requests/${encodeURIComponent(requestId)}`,
  )
}
export async function createFormOptionListChangeRequest(
  sessionId: string,
  input: FormOptionListDefinition & { reason: string },
): Promise<FormOptionListChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    '/api/administration/form-option-list-change-requests',
    input,
  )
}
export async function transitionFormOptionListChangeRequest(
  sessionId: string,
  requestId: string,
  action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel',
  input: { note?: string | null; expectedVersion: number },
): Promise<FormOptionListChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    `/api/administration/form-option-list-change-requests/${encodeURIComponent(requestId)}/${action}`,
    input,
  )
}
export type ClinicalAlertRuleItem = {
  key: string
  title: string
  triggerType: 'patient' | 'encounter' | 'appointment'
  targetType: 'banner' | 'reminder'
  severity: 'info' | 'warning' | 'critical'
  message: string
  sequence: number
  active: boolean
}
export async function getClinicalAlertRules(
  sessionId: string,
): Promise<{ rules: ClinicalAlertRuleItem[] }> {
  return clinicianGet(sessionId, '/api/administration/clinical-alert-rules')
}
export type ClinicalAlertRuleChangeRequest = {
  requestId: string
  ruleKey: string
  status: GovernanceStatus
  version: number
  reason: string
  proposedDefinition: ClinicalAlertRuleItem
  baselineDefinition?: ClinicalAlertRuleItem | null
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type ClinicalAlertRuleChangeRequestDetail = {
  request: ClinicalAlertRuleChangeRequest
  activeRule?: ClinicalAlertRuleItem | null
  events: GovernanceEvent[]
}
export type ClinicalAlertRuleChangeRequestsResponse = {
  requests: ClinicalAlertRuleChangeRequest[]
  total: number
  returned: number
  offset: number
  limit: number
  status: GovernanceStatus | 'all' | 'open'
}
export async function getClinicalAlertRuleChangeRequests(
  sessionId: string,
  options: FormChangeRequestListOptions = {},
): Promise<ClinicalAlertRuleChangeRequestsResponse> {
  const parameters = new URLSearchParams({
    status: options.status ?? 'open',
    offset: String(options.offset ?? 0),
    limit: String(options.limit ?? 25),
  })
  return clinicianGet(
    sessionId,
    `/api/administration/clinical-alert-rule-change-requests?${parameters.toString()}`,
  )
}
export async function createClinicalAlertRuleChangeRequest(
  sessionId: string,
  input: ClinicalAlertRuleItem & { reason: string },
): Promise<ClinicalAlertRuleChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    '/api/administration/clinical-alert-rule-change-requests',
    input,
  )
}
export async function getClinicalAlertRuleChangeRequest(
  sessionId: string,
  requestId: string,
): Promise<ClinicalAlertRuleChangeRequestDetail> {
  return clinicianGet(
    sessionId,
    `/api/administration/clinical-alert-rule-change-requests/${encodeURIComponent(requestId)}`,
  )
}
export async function transitionClinicalAlertRuleChangeRequest(
  sessionId: string,
  requestId: string,
  action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel',
  input: { note?: string | null; expectedVersion: number },
): Promise<ClinicalAlertRuleChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    `/api/administration/clinical-alert-rule-change-requests/${encodeURIComponent(requestId)}/${action}`,
    input,
  )
}
export async function saveClinicalAlertRule(
  sessionId: string,
  key: string,
  input: Omit<ClinicalAlertRuleItem, 'key'>,
): Promise<{ rules: ClinicalAlertRuleItem[] }> {
  return clinicianPut(
    sessionId,
    `/api/administration/clinical-alert-rules/${key}`,
    input,
  )
}
export type ClinicalAlertRuleRevision = {
  revisionId: number
  title: string
  triggerType: ClinicalAlertRuleItem['triggerType']
  targetType: ClinicalAlertRuleItem['targetType']
  severity: ClinicalAlertRuleItem['severity']
  message: string
  sequence: number
  active: boolean
  action: string
  restoredFromRevisionId?: number | null
  occurredAt: string
  username: string
}
export type ClinicalAlertRuleHistory = {
  rule: ClinicalAlertRuleItem
  revisions: ClinicalAlertRuleRevision[]
}
export async function getClinicalAlertRuleHistory(
  sessionId: string,
  key: string,
): Promise<ClinicalAlertRuleHistory> {
  return clinicianGet(
    sessionId,
    `/api/administration/clinical-alert-rules/${key}/history`,
  )
}
export async function rollbackClinicalAlertRule(
  sessionId: string,
  key: string,
  revisionId: number,
): Promise<ClinicalAlertRuleHistory> {
  return clinicianPost(
    sessionId,
    `/api/administration/clinical-alert-rules/${key}/revisions/${revisionId}/rollback`,
    {},
  )
}
export type ModuleCatalogItem = {
  key: string
  displayName: string
  category: string
  status: string
  description: string
  canChangeStatus: boolean
}
export async function getModuleCatalog(
  sessionId: string,
): Promise<{ modules: ModuleCatalogItem[] }> {
  return clinicianGet(sessionId, '/api/administration/modules')
}
export type ModuleChangeRequest = {
  requestId: string
  moduleKey: string
  proposedStatus: 'enabled' | 'disabled'
  baselineStatus: 'enabled' | 'disabled'
  reason: string
  status: GovernanceStatus
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type ModuleChangeRequestsResponse = {
  requests: ModuleChangeRequest[]
  total: number
  status: GovernanceStatus | 'all' | 'open'
}
export async function getModuleChangeRequests(
  sessionId: string,
  status: GovernanceStatus | 'all' | 'open' = 'open',
): Promise<ModuleChangeRequestsResponse> {
  return clinicianGet(
    sessionId,
    `/api/administration/module-change-requests?status=${encodeURIComponent(status)}`,
  )
}
export type ModuleChangeRequestDetail = {
  request: ModuleChangeRequest
  module: ModuleCatalogItem
  events: GovernanceEvent[]
}
export async function getModuleChangeRequest(
  sessionId: string,
  requestId: string,
): Promise<ModuleChangeRequestDetail> {
  return clinicianGet(
    sessionId,
    `/api/administration/module-change-requests/${encodeURIComponent(requestId)}`,
  )
}
export async function createModuleChangeRequest(
  sessionId: string,
  input: { moduleKey: string; status: 'enabled' | 'disabled'; reason: string },
): Promise<{ request: ModuleChangeRequest }> {
  return clinicianPost(
    sessionId,
    '/api/administration/module-change-requests',
    input,
  )
}
export async function transitionModuleChangeRequest(
  sessionId: string,
  requestId: string,
  action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel',
  input: { note?: string | null; expectedVersion: number },
): Promise<{ request: ModuleChangeRequest }> {
  return clinicianPost(
    sessionId,
    `/api/administration/module-change-requests/${encodeURIComponent(requestId)}/${action}`,
    input,
  )
}
export type ModuleCatalogRevision = {
  revisionId: number
  displayName: string
  category: string
  status: string
  description: string
  action: string
  restoredFromRevisionId?: number | null
  occurredAt: string
  username: string
}
export type ModuleCatalogHistory = {
  module: ModuleCatalogItem
  revisions: ModuleCatalogRevision[]
}
export async function getModuleCatalogHistory(
  sessionId: string,
  key: string,
): Promise<ModuleCatalogHistory> {
  return clinicianGet(sessionId, `/api/administration/modules/${key}/history`)
}
export async function updateModuleCatalogStatus(
  sessionId: string,
  key: string,
  status: 'enabled' | 'disabled',
): Promise<ModuleCatalogHistory> {
  return clinicianPut(sessionId, `/api/administration/modules/${key}/status`, {
    status,
  })
}
export async function rollbackModuleCatalog(
  sessionId: string,
  key: string,
  revisionId: number,
): Promise<ModuleCatalogHistory> {
  return clinicianPost(
    sessionId,
    `/api/administration/modules/${key}/revisions/${revisionId}/rollback`,
    {},
  )
}
export type ApiClientRegistryItem = {
  key: string
  displayName: string
  redirectUri: string
  scopes: string
  active: boolean
}
export async function getApiClients(
  sessionId: string,
): Promise<{ clients: ApiClientRegistryItem[] }> {
  return clinicianGet(sessionId, '/api/administration/api-clients')
}
export type ApiClientChangeRequest = {
  requestId: string
  clientKey: string
  changeKind: 'create' | 'update'
  proposedDefinition: ApiClientRegistryItem
  baselineDefinition?: ApiClientRegistryItem | null
  baselineUpdatedAt?: string | null
  reason: string
  status: GovernanceStatus
  version: number
  createdAt: string
  createdBy: string
  updatedAt: string
  updatedBy: string
}
export type ApiClientChangeRequestDetail = {
  request: ApiClientChangeRequest
  activeClient?: ApiClientRegistryItem | null
  events: GovernanceEvent[]
}
export type ApiClientChangeRequestsResponse = {
  requests: ApiClientChangeRequest[]
  total: number
  status: GovernanceStatus | 'all' | 'open'
  counts: {
    draft: number
    submitted: number
    approved: number
    rejected: number
    activated: number
    cancelled: number
  }
}
export async function getApiClientChangeRequests(
  sessionId: string,
  status: GovernanceStatus | 'all' | 'open' = 'open',
): Promise<ApiClientChangeRequestsResponse> {
  return clinicianGet(
    sessionId,
    `/api/administration/api-client-change-requests?status=${encodeURIComponent(status)}`,
  )
}
export async function getApiClientChangeRequest(
  sessionId: string,
  requestId: string,
): Promise<ApiClientChangeRequestDetail> {
  return clinicianGet(
    sessionId,
    `/api/administration/api-client-change-requests/${encodeURIComponent(requestId)}`,
  )
}
export async function createApiClientChangeRequest(
  sessionId: string,
  input: ApiClientRegistryItem & { reason: string },
): Promise<ApiClientChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    '/api/administration/api-client-change-requests',
    input,
  )
}
export async function transitionApiClientChangeRequest(
  sessionId: string,
  requestId: string,
  action: 'submit' | 'approve' | 'reject' | 'activate' | 'cancel',
  input: { note?: string | null; expectedVersion: number },
): Promise<ApiClientChangeRequestDetail> {
  return clinicianPost(
    sessionId,
    `/api/administration/api-client-change-requests/${encodeURIComponent(requestId)}/${action}`,
    input,
  )
}
export async function saveApiClient(
  sessionId: string,
  key: string,
  input: Omit<ApiClientRegistryItem, 'key'>,
): Promise<{ clients: ApiClientRegistryItem[] }> {
  return clinicianPut(
    sessionId,
    `/api/administration/api-clients/${key}`,
    input,
  )
}
export type ApiClientRegistryRevision = {
  revisionId: number
  displayName: string
  redirectUri: string
  scopes: string
  active: boolean
  action: string
  restoredFromRevisionId?: number | null
  occurredAt: string
  username: string
}
export type ApiClientRegistryHistory = {
  client: ApiClientRegistryItem
  revisions: ApiClientRegistryRevision[]
}
export async function getApiClientRegistryHistory(
  sessionId: string,
  key: string,
): Promise<ApiClientRegistryHistory> {
  return clinicianGet(
    sessionId,
    `/api/administration/api-clients/${key}/history`,
  )
}
export async function rollbackApiClientRegistry(
  sessionId: string,
  key: string,
  revisionId: number,
): Promise<ApiClientRegistryHistory> {
  return clinicianPost(
    sessionId,
    `/api/administration/api-clients/${key}/revisions/${revisionId}/rollback`,
    {},
  )
}

export type PhiAccessAuditEvent = {
  auditId: string
  occurredAt: string
  username: string
  httpMethod: string
  requestPath: string
  requiredPermission: string
  authorized: boolean
  responseStatus: number
}

export type PhiAccessAuditResponse = {
  totalEvents: number
  authorizedEvents: number
  deniedEvents: number
  events: PhiAccessAuditEvent[]
}

export type PhiAccessAuditFilters = {
  limit?: number
  username?: string
  from?: string
  to?: string
}
export async function getPhiAccessAudit(
  sessionId: string,
  filters: PhiAccessAuditFilters = {},
  signal?: AbortSignal,
): Promise<PhiAccessAuditResponse> {
  const query = new URLSearchParams({ limit: String(filters.limit ?? 50) })
  if (filters.username) query.set('username', filters.username)
  if (filters.from) query.set('from', filters.from)
  if (filters.to) query.set('to', filters.to)
  return clinicianGet(
    sessionId,
    `/api/administration/audit/phi?${query}`,
    signal,
  )
}
export async function downloadPhiAccessAuditCsv(
  sessionId: string,
  filters: PhiAccessAuditFilters = {},
): Promise<Blob> {
  const query = new URLSearchParams({ limit: String(filters.limit ?? 200) })
  if (filters.username) query.set('username', filters.username)
  if (filters.from) query.set('from', filters.from)
  if (filters.to) query.set('to', filters.to)
  const response = await fetch(
    `${apiBaseUrl}/api/administration/audit/phi/export?${query}`,
    {
      headers: { 'X-AvenChart-Session': sessionId },
    },
  )
  if (!response.ok)
    throw new Error(`PHI access audit export failed with ${response.status}`)
  return response.blob()
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

export type AdministrationFacilityMutationResponse = {
  id: number
  detail: AdministrationDirectoryResponse
}

export async function createAdministrationFacility(
  sessionId: string,
  body: AdministrationFacilityMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationFacilityMutationResponse> {
  return clinicianPost(
    sessionId,
    '/api/administration/facilities',
    body,
    signal,
  )
}

export async function updateAdministrationFacility(
  sessionId: string,
  facilityId: number,
  body: AdministrationFacilityMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationFacilityMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/administration/facilities/${facilityId}`,
    body,
    signal,
  )
}

export async function deleteAdministrationFacility(
  sessionId: string,
  facilityId: number,
  signal?: AbortSignal,
): Promise<void> {
  return clinicianDelete(
    sessionId,
    `/api/administration/facilities/${facilityId}`,
    signal,
  )
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

export type AdministrationUserMutationResponse = {
  id: number
  detail: AdministrationDirectoryResponse
}

export async function createAdministrationUser(
  sessionId: string,
  body: AdministrationUserMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationUserMutationResponse> {
  return clinicianPost(sessionId, '/api/administration/users', body, signal)
}

export async function updateAdministrationUser(
  sessionId: string,
  userId: number,
  body: AdministrationUserMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationUserMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/administration/users/${userId}`,
    body,
    signal,
  )
}

export async function deleteAdministrationUser(
  sessionId: string,
  userId: number,
  signal?: AbortSignal,
): Promise<void> {
  return clinicianDelete(
    sessionId,
    `/api/administration/users/${userId}`,
    signal,
  )
}

export type AdministrationAccessPermissionMutationInput = {
  groupValue: string
  sectionValue: string
  permissionValue: string
  returnValue: 'addonly' | 'view' | 'write' | 'wsome'
}

export type AdministrationAccessPermissionMutationResponse = {
  groupValue: string
  sectionValue: string
  permissionValue: string
  returnValue?: string | null
  detail: AdministrationDirectoryResponse
}

export type AdministrationAccessUserMembershipMutationInput = {
  userValue: string
  groupValue: string
}

export type AdministrationAccessUserMembershipMutationResponse = {
  userValue: string
  groupValue: string
  detail: AdministrationDirectoryResponse
}

export async function grantAdministrationAccessPermission(
  sessionId: string,
  body: AdministrationAccessPermissionMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationAccessPermissionMutationResponse> {
  return clinicianPut(
    sessionId,
    '/api/administration/access-control/group-permissions',
    body,
    signal,
  )
}

export async function revokeAdministrationAccessPermission(
  sessionId: string,
  groupValue: string,
  sectionValue: string,
  permissionValue: string,
  signal?: AbortSignal,
): Promise<AdministrationAccessPermissionMutationResponse> {
  return clinicianDeleteJson(
    sessionId,
    `/api/administration/access-control/group-permissions/${encodeURIComponent(groupValue)}/${encodeURIComponent(sectionValue)}/${encodeURIComponent(permissionValue)}`,
    signal,
  )
}

export async function grantAdministrationAccessMembership(
  sessionId: string,
  body: AdministrationAccessUserMembershipMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationAccessUserMembershipMutationResponse> {
  return clinicianPut(
    sessionId,
    '/api/administration/access-control/user-memberships',
    body,
    signal,
  )
}

export async function revokeAdministrationAccessMembership(
  sessionId: string,
  userValue: string,
  groupValue: string,
  signal?: AbortSignal,
): Promise<AdministrationAccessUserMembershipMutationResponse> {
  return clinicianDeleteJson(
    sessionId,
    `/api/administration/access-control/user-memberships/${encodeURIComponent(userValue)}/${encodeURIComponent(groupValue)}`,
    signal,
  )
}

export type AdministrationPortalProfileReviewMutationResponse = {
  detail: AdministrationDirectoryResponse
}

export async function acceptAdministrationPortalProfileReview(
  sessionId: string,
  requestId: string,
  signal?: AbortSignal,
): Promise<AdministrationPortalProfileReviewMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/administration/portal-activity/profile-reviews/${encodeURIComponent(requestId)}/accept`,
    {},
    signal,
  )
}

export async function revertAdministrationPortalProfileReview(
  sessionId: string,
  requestId: string,
  signal?: AbortSignal,
): Promise<AdministrationPortalProfileReviewMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/administration/portal-activity/profile-reviews/${encodeURIComponent(requestId)}/revert`,
    {},
    signal,
  )
}

export async function getLoginAudit(
  sessionId: string,
  limit?: number,
  signal?: AbortSignal,
): Promise<AuthenticationAuditLoginSummary> {
  const q = limit ? `?limit=${limit}` : ''
  return clinicianGet(sessionId, `/api/auth/login-audit${q}`, signal)
}

export type AuthenticationAuditEvent = {
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

export type AuthenticationAuditLoginSummary = {
  totalEvents: number
  successfulLogins: number
  failedLogins: number
  events: AuthenticationAuditEvent[]
}

export type AuthenticationSessionAuditItem = {
  username: string
  role: string
  createdAt: string
  lastSeenAt: string
  expiresAt: string
  endedAt?: string | null
  active: boolean
  sessionSource: string
}

export type AuthenticationActivityAuditResponse =
  AuthenticationAuditLoginSummary & {
    activeSessions: number
    endedSessions: number
    sessions: AuthenticationSessionAuditItem[]
  }

export async function getAuthenticationActivityAudit(
  sessionId: string,
  limit = 25,
  signal?: AbortSignal,
): Promise<AuthenticationActivityAuditResponse> {
  return clinicianGet(
    sessionId,
    `/api/auth/activity-audit?limit=${encodeURIComponent(String(limit))}`,
    signal,
  )
}

// ── Write helpers ─────────────────────────────────────────────────────────────

async function clinicianDelete(
  sessionId: string,
  path: string,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'DELETE',
    headers: clinicianHeaders(sessionId),
    signal,
  })
  await requireSuccessfulResponse(response, `DELETE ${path}`, 'clinician')
}

async function clinicianDeleteJson<T>(
  sessionId: string,
  path: string,
  signal?: AbortSignal,
): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: 'DELETE',
    headers: clinicianHeaders(sessionId),
    signal,
  })
  await requireSuccessfulResponse(response, `DELETE ${path}`, 'clinician')
  return response.json()
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
  return clinicianPost(sessionId, '/api/encounters', body, signal)
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
  return clinicianPost(
    sessionId,
    `/api/encounters/${encounterId}/vitals`,
    body,
    signal,
  )
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
  return clinicianPost(
    sessionId,
    `/api/encounters/${encounterId}/soap-notes`,
    body,
    signal,
  )
}

export async function getEncounterSoapNoteTemplates(
  sessionId: string,
  signal?: AbortSignal,
): Promise<EncounterSoapNoteTemplateCatalog> {
  return clinicianGet(sessionId, '/api/encounters/soap-note-templates', signal)
}

export async function signEncounter(
  sessionId: string,
  encounterId: number,
  body: {
    signerUsername: string
    signedAt: string
    isLock: boolean
    amendment?: string | null
  },
  signal?: AbortSignal,
): Promise<{ id: number; detail: EncounterDetail }> {
  return clinicianPut(
    sessionId,
    `/api/encounters/${encounterId}/sign`,
    body,
    signal,
  )
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
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/problems/${problemId}/deactivate`,
    { comments },
    signal,
  )
}

export async function deleteProblem(
  sessionId: string,
  problemId: string,
  signal?: AbortSignal,
): Promise<void> {
  return clinicianDelete(
    sessionId,
    `/api/clinical-lists/problems/${problemId}`,
    signal,
  )
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
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/allergies/${allergyId}/deactivate`,
    { comments },
    signal,
  )
}

export async function deleteAllergy(
  sessionId: string,
  allergyId: string,
  signal?: AbortSignal,
): Promise<void> {
  return clinicianDelete(
    sessionId,
    `/api/clinical-lists/allergies/${allergyId}`,
    signal,
  )
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
  return clinicianPost(
    sessionId,
    '/api/clinical-lists/medications',
    body,
    signal,
  )
}

export async function deactivateMedication(
  sessionId: string,
  medicationId: string,
  comments: string,
  expectedVersion: number,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/medications/${medicationId}/deactivate`,
    { comments, expectedVersion },
    signal,
  )
}

export async function restoreMedication(
  sessionId: string,
  medicationId: string,
  reason: string,
  expectedVersion: number,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/medications/${medicationId}/restore`,
    { reason, expectedVersion },
    signal,
  )
}

export type UpdateMedicationInput = {
  title: string
  diagnosis?: string | null
  date: string
  comments?: string | null
  reason: string
  expectedVersion: number
}

export function updateMedication(
  sessionId: string,
  medicationId: string,
  body: UpdateMedicationInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/medications/${medicationId}`,
    body,
    signal,
  )
}

export function getMedicationLifecycleHistory(
  sessionId: string,
  medicationId: string,
  signal?: AbortSignal,
): Promise<MedicationLifecycleHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/clinical-lists/medications/${medicationId}/lifecycle-history`,
    signal,
  )
}

export type CreatePrescriptionInput = {
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

export async function createPrescription(
  sessionId: string,
  body: CreatePrescriptionInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPost(
    sessionId,
    '/api/clinical-lists/prescriptions',
    body,
    signal,
  )
}

export async function deactivatePrescription(
  sessionId: string,
  prescriptionId: string,
  body: { endDate: string; note: string },
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/prescriptions/${prescriptionId}/deactivate`,
    body,
    signal,
  )
}

// ── Prescription refill and audit ─────────────────────────────────────────────

export type PrescriptionUpdateInput = {
  expectedVersion: string
  startDate: string
  dosage: string
  quantity: string
  doseAmount?: number | null
  doseUnit?: string | null
  frequency?: string | null
  durationDays?: number | null
  route?: string | null
  refills: number
  diagnosis?: string | null
  note?: string | null
  editReason: string
}

export async function updatePrescription(
  sessionId: string,
  prescriptionId: string,
  body: PrescriptionUpdateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}`,
    body,
    signal,
  )
}

export type PrescriptionRefillInput = {
  refillDate: string
  additionalRefills: number
  note: string
}

export async function refillPrescription(
  sessionId: string,
  prescriptionId: string,
  body: PrescriptionRefillInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}/refill`,
    body,
    signal,
  )
}

export async function approvePrescriptionRefillRequest(
  sessionId: string,
  messageId: number,
  body: PrescriptionRefillInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/prescription-refill-requests/${messageId}/approve`,
    body,
    signal,
  )
}

export type PrescriptionRefillDecisionInput = {
  action: 'deny' | 'request-clarification' | 'complete'
  response: string
}

export type PrescriptionRefillDecisionResponse = {
  messageId: number
  prescriptionId: string
  status: string
  staffResponse: string
}

export async function decidePrescriptionRefillRequest(
  sessionId: string,
  messageId: number,
  body: PrescriptionRefillDecisionInput,
  signal?: AbortSignal,
): Promise<PrescriptionRefillDecisionResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/prescription-refill-requests/${messageId}/decision`,
    body,
    signal,
  )
}

export type PrescriptionPharmacyRouteInput = {
  pharmacyId: number
  sentAt: string
  note: string
}

export type PrescriptionPharmacyRouteResponse = {
  id: string
  routed: boolean
  failureReason?: string | null
  detail: ClinicalListsResponse
}

export async function routePrescriptionToPharmacy(
  sessionId: string,
  prescriptionId: string,
  body: PrescriptionPharmacyRouteInput,
  signal?: AbortSignal,
): Promise<PrescriptionPharmacyRouteResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}/route-pharmacy`,
    body,
    signal,
  )
}

export type ClinicalPrescriptionAuditEvent = {
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

export type ClinicalPrescriptionAuditHistory = {
  prescriptionId: string
  eventCount: number
  events: ClinicalPrescriptionAuditEvent[]
}

export async function getPrescriptionAuditHistory(
  sessionId: string,
  prescriptionId: string,
  signal?: AbortSignal,
): Promise<ClinicalPrescriptionAuditHistory> {
  return clinicianGet(
    sessionId,
    `/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}/audit-history`,
    signal,
  )
}

// ── Lab report sign ───────────────────────────────────────────────────────────

export type ProcedureMutationResponse = {
  id: number
  detail: ProcedureResultsResponse
}

export type CriticalLabResultQueueResponse = {
  totalOpen: number
  results: Array<{
    resultId: number
    reportId: number
    patientId: string
    patientDisplayName: string
    code?: string | null
    text?: string | null
    result?: string | null
    units?: string | null
    abnormal?: string | null
    resultDate: string
    acknowledgementStatus: string
    acknowledgementVersion: number
    acknowledgedBy?: string | null
    acknowledgedAt?: string | null
  }>
}

export function getCriticalLabResultQueue(
  sessionId: string,
  signal?: AbortSignal,
): Promise<CriticalLabResultQueueResponse> {
  return clinicianGet(
    sessionId,
    '/api/procedures/critical-result-queue',
    signal,
  )
}

export async function acknowledgeCriticalLabResult(
  sessionId: string,
  resultId: number,
  body: { expectedVersion: number; reason: string },
): Promise<void> {
  await clinicianPut<{ acknowledged: boolean }>(
    sessionId,
    `/api/procedures/results/${resultId}/critical-acknowledgement`,
    body,
  )
}

export type ProcedureReportReviewHistoryResponse = {
  reportId: number
  reviewVersion: number
  events: Array<{
    eventId: number
    action: string
    previousStatus?: string | null
    currentStatus: string
    assignedTo?: string | null
    actor: string
    reason?: string | null
    expectedVersion: number
    resultingVersion: number
    occurredAt: string
  }>
}

export function getLabReportReviewHistory(
  sessionId: string,
  reportId: number,
  signal?: AbortSignal,
): Promise<ProcedureReportReviewHistoryResponse> {
  return clinicianGet(
    sessionId,
    `/api/procedures/reports/${reportId}/review-history`,
    signal,
  )
}

export async function signLabReport(
  sessionId: string,
  reportId: number,
  body: { expectedReviewVersion: number; reason: string },
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  return clinicianPut<ProcedureMutationResponse>(
    sessionId,
    `/api/procedures/reports/${reportId}/sign`,
    body,
    signal,
  )
}

export async function assignLabReportReviewer(
  sessionId: string,
  reportId: number,
  body: { assignedTo: string; expectedReviewVersion: number; reason: string },
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  return clinicianPut<ProcedureMutationResponse>(
    sessionId,
    `/api/procedures/reports/${reportId}/review-assignment`,
    body,
    signal,
  )
}

export async function reopenLabReportReview(
  sessionId: string,
  reportId: number,
  body: { expectedReviewVersion: number; reason: string },
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  return clinicianPut<ProcedureMutationResponse>(
    sessionId,
    `/api/procedures/reports/${reportId}/reopen-review`,
    body,
    signal,
  )
}

export async function denyLabReportReview(
  sessionId: string,
  reportId: number,
  body: { expectedReviewVersion: number; reason: string },
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  return clinicianPut<ProcedureMutationResponse>(
    sessionId,
    `/api/procedures/reports/${reportId}/deny-review`,
    body,
    signal,
  )
}

export type ProcedureReportBulkSignResponse = {
  requestedCount: number
  signedCount: number
  signedReportIds: number[]
  reviewedBy: string
  reviewedAt: string
}

export async function bulkSignLabReports(
  sessionId: string,
  body: {
    reports: Array<{ reportId: number; expectedReviewVersion: number }>
    reason: string
  },
  signal?: AbortSignal,
): Promise<ProcedureReportBulkSignResponse> {
  return clinicianPut<ProcedureReportBulkSignResponse>(
    sessionId,
    '/api/procedures/reports/bulk-sign',
    body,
    signal,
  )
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
): Promise<PatientMessagesResponse> {
  const result = await clinicianPost<PatientMessageMutationResponse>(
    sessionId,
    '/api/messages',
    input,
    signal,
  )
  return result.detail
}

// ── Patient mutations ─────────────────────────────────────────────────────────

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

export type PatientAdministrationUpdate = {
  contact: PatientContactUpdate
  demographics: PatientDemographicsUpdate
  expectedVersion: number
}

export async function updatePatientAdministration(
  sessionId: string,
  patientId: string,
  body: PatientAdministrationUpdate,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPut(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/administration`,
    body,
    signal,
  )
}

export type PatientInsuranceMutationInput = {
  type: string
  provider: string
  planName: string
  policyNumber: string
  groupNumber: string
  relationship: string
  subscriberFirstName: string
  subscriberLastName: string
  subscriberDateOfBirth: string
  subscriberSex: string
}

export async function createPatientInsurance(
  sessionId: string,
  patientId: string,
  body: PatientInsuranceMutationInput,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPost(
    sessionId,
    `/api/patients/${encodeURIComponent(patientId)}/insurance`,
    body,
    signal,
  )
}

export async function updatePatientInsurance(
  sessionId: string,
  insuranceId: string,
  body: PatientInsuranceMutationInput,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPut(
    sessionId,
    `/api/patients/insurance/${encodeURIComponent(insuranceId)}`,
    body,
    signal,
  )
}

export async function deletePatientInsurance(
  sessionId: string,
  insuranceId: string,
  signal?: AbortSignal,
): Promise<void> {
  await clinicianDelete(
    sessionId,
    `/api/patients/insurance/${encodeURIComponent(insuranceId)}`,
    signal,
  )
}

export type PatientRegistrationInput = {
  pubpid: string
  firstName: string
  lastName: string
  preferredName: string
  sex: string
  dateOfBirth: string
  street: string
  city: string
  state: string
  postalCode: string
  phoneHome: string
  phoneCell: string
  email: string
  maritalStatus: string
  occupation: string
  race: string
  ethnicity: string
  hipaaAllowSms: string
  hipaaAllowEmail: string
}

export type PatientRegistrationSubmission = PatientRegistrationInput & {
  duplicateReviewAcknowledged?: boolean
  duplicateReviewReason?: string
}

export async function createPatient(
  sessionId: string,
  body: PatientRegistrationSubmission,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  return clinicianPost(sessionId, '/api/patients', body, signal)
}

// ── Appointment mutations ─────────────────────────────────────────────────────

export type AppointmentCreateInput = {
  patientId: string
  title: string
  date: string
  startTime: string
  durationMinutes: number
  providerId?: number | null
  facilityId?: number | null
  categoryId?: number | null
  room?: string | null
  comments?: string | null
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

export async function createAppointment(
  sessionId: string,
  body: AppointmentCreateInput,
  signal?: AbortSignal,
): Promise<AppointmentListItem> {
  return clinicianPost(sessionId, '/api/appointments', body, signal)
}

export async function validateAppointmentAvailability(
  sessionId: string,
  body: AppointmentAvailabilityValidationInput,
  signal?: AbortSignal,
): Promise<AppointmentAvailabilityValidationResponse> {
  return clinicianPost(
    sessionId,
    '/api/appointments/availability/validate',
    body,
    signal,
  )
}

// ── Immunization mutations ────────────────────────────────────────────────────

export type ImmunizationCreateInput = {
  patientId: string
  vaccine: string
  administeredAt: string
  manufacturer?: string | null
  lotNumber?: string | null
}

export async function createImmunization(
  sessionId: string,
  body: ImmunizationCreateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPost(
    sessionId,
    '/api/clinical-lists/immunizations',
    body,
    signal,
  )
}

export async function markImmunizationEnteredInError(
  sessionId: string,
  immunizationId: number,
  note: string,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  return clinicianPut(
    sessionId,
    `/api/clinical-lists/immunizations/${immunizationId}/entered-in-error`,
    { note },
    signal,
  )
}

export async function deleteImmunization(
  sessionId: string,
  immunizationId: number,
  signal?: AbortSignal,
): Promise<void> {
  return clinicianDelete(
    sessionId,
    `/api/clinical-lists/immunizations/${immunizationId}`,
    signal,
  )
}

// ── Portal profile mutations ──────────────────────────────────────────────────

export type PatientPortalProfileDemographics = {
  firstName: string
  lastName: string
  preferredName?: string | null
  dateOfBirth?: string | null
  sex?: string | null
  phoneHome?: string | null
  phoneCell?: string | null
  email?: string | null
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  hipaaAllowSms?: string | null
  hipaaAllowEmail?: string | null
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

export type PatientPortalProfileResponse = {
  authenticated: boolean
  displayName: string
  portalUsername: string
  hasPendingProfileChanges: boolean
  demographics: PatientPortalProfileDemographics
  pendingChange?: PatientPortalProfileChangeRequest | null
  failureReason?: string | null
}

export async function getPatientPortalProfile(
  sessionId: string,
  signal?: AbortSignal,
): Promise<PatientPortalProfileResponse> {
  const response = await fetch(`${apiBaseUrl}/api/patient-portal/profile`, {
    headers: { 'X-AvenChart-Patient-Portal-Session': sessionId },
    signal,
  })
  if (!response.ok)
    throw new Error(`Patient portal profile failed with ${response.status}`)
  return response.json()
}

export type PatientPortalProfileChangeInput = {
  phoneHome?: string | null
  phoneCell?: string | null
  email?: string | null
  hipaaAllowSms?: string | null
  hipaaAllowEmail?: string | null
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
}

export async function submitPatientPortalProfileChange(
  sessionId: string,
  body: PatientPortalProfileChangeInput,
  signal?: AbortSignal,
): Promise<PatientPortalProfileResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/patient-portal/profile/changes`,
    {
      method: 'POST',
      headers: {
        'content-type': 'application/json',
        'X-AvenChart-Patient-Portal-Session': sessionId,
      },
      body: JSON.stringify(body),
      signal,
    },
  )
  if (!response.ok)
    throw new Error(
      `Patient portal profile request failed with ${response.status}`,
    )
  return response.json()
}
