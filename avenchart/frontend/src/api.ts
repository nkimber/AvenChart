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

export type PatientChartSummary = PatientListItem & {
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  hipaaAllowSms?: string | null
  hipaaAllowEmail?: string | null
  maritalStatus?: string | null
  occupation?: string | null
  portalEnabled: boolean
  registrationDate: string
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

export type AppointmentListItem = {
  id: string
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
  providerName?: string | null
  facilityName?: string | null
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
  categoryId?: number | null
  room?: string | null
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

export type EncounterDetail = EncounterListItem & {
  firstName: string
  lastName: string
  sex?: string | null
  dateOfBirth: string
  dateTime: string
  vitals?: EncounterVitals | null
  soapNote?: EncounterSoapNote | null
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
}

export type AllergyListItem = {
  id: string
  title: string
  reaction?: string | null
  severity?: string | null
  date?: string | null
  comments?: string | null
}

export type MedicationListItem = {
  id: string
  title: string
  diagnosis?: string | null
  date?: string | null
  comments?: string | null
}

export type PrescriptionListItem = {
  id: string
  drug: string
  dosage?: string | null
  route?: string | null
  diagnosis?: string | null
  startDate?: string | null
  encounter?: number | null
  providerName?: string | null
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
  prescriptions: PrescriptionListItem[]
}

export type PatientMessageItem = {
  id: string
  date?: string | null
  title?: string | null
  body?: string | null
  status?: string | null
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
  reportDate: string
  status?: string | null
  results: ProcedureResultItem[]
}

export type ProcedureOrderItem = {
  id: number
  encounter?: number | null
  providerName?: string | null
  orderDate: string
  code?: string | null
  name?: string | null
  diagnosis?: string | null
  orderStatus?: string | null
  reports: ProcedureReportItem[]
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
  orders: ProcedureOrderItem[]
}

export type BillingLineItem = {
  id: string
  encounter: number
  billingDate: string
  codeType?: string | null
  code?: string | null
  codeText?: string | null
  fee?: number | null
  justify?: string | null
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
  lines: BillingLineItem[]
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
  encounters: BillingEncounterItem[]
}

export type AdministrationDirectoryCounts = {
  users: number
  providers: number
  calendarUsers: number
  facilities: number
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

export type AdministrationFacilityItem = {
  id: number
  code: string
  name: string
  phone?: string | null
  street?: string | null
  city?: string | null
  state?: string | null
  postalCode?: string | null
  color?: string | null
}

export type AdministrationDirectoryResponse = {
  datasetId: string
  datasetVersion: string
  counts: AdministrationDirectoryCounts
  users: AdministrationUserItem[]
  facilities: AdministrationFacilityItem[]
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

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5001'

export async function searchPatients(search: string, signal?: AbortSignal): Promise<PatientSearchResponse> {
  const params = new URLSearchParams()
  if (search.trim()) {
    params.set('search', search.trim())
  }
  params.set('limit', '25')

  const response = await fetch(`${apiBaseUrl}/api/patients?${params.toString()}`, { signal })
  if (!response.ok) {
    throw new Error(`Patient search failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientChart(canonicalId: string, signal?: AbortSignal): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(canonicalId)}`, { signal })
  if (!response.ok) {
    throw new Error(`Patient chart load failed with ${response.status}`)
  }

  return response.json()
}

export async function updatePatientContact(
  patientId: string,
  contact: PatientContactUpdate,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/contact`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(contact),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient contact update failed with ${response.status}`)
  }

  return response.json()
}

export async function searchAppointments(
  patientId: string,
  fromDate: string,
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

  const response = await fetch(`${apiBaseUrl}/api/appointments?${params.toString()}`, { signal })
  if (!response.ok) {
    throw new Error(`Appointment search failed with ${response.status}`)
  }

  return response.json()
}

export async function getAppointmentDetail(
  appointmentId: string,
  signal?: AbortSignal,
): Promise<AppointmentDetail> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}`, { signal })
  if (!response.ok) {
    throw new Error(`Appointment detail load failed with ${response.status}`)
  }

  return response.json()
}

export async function createAppointment(
  appointment: AppointmentCreateInput,
  signal?: AbortSignal,
): Promise<AppointmentDetail> {
  const response = await fetch(`${apiBaseUrl}/api/appointments`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(appointment),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Appointment create failed with ${response.status}`)
  }

  return response.json()
}

export async function updateAppointmentStatus(
  appointmentId: string,
  update: AppointmentStatusUpdate,
  signal?: AbortSignal,
): Promise<AppointmentDetail> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}/status`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Appointment status update failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteAppointment(appointmentId: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/appointments/${encodeURIComponent(appointmentId)}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Appointment delete failed with ${response.status}`)
  }
}

export async function searchEncounters(
  patientId: string,
  fromDate: string,
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

  const response = await fetch(`${apiBaseUrl}/api/encounters?${params.toString()}`, { signal })
  if (!response.ok) {
    throw new Error(`Encounter search failed with ${response.status}`)
  }

  return response.json()
}

export async function getEncounterDetail(encounter: number, signal?: AbortSignal): Promise<EncounterDetail> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}`, { signal })
  if (!response.ok) {
    throw new Error(`Encounter detail load failed with ${response.status}`)
  }

  return response.json()
}

export async function getClinicalLists(patientId: string, signal?: AbortSignal): Promise<ClinicalListsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/${encodeURIComponent(patientId.trim())}`, { signal })
  if (!response.ok) {
    throw new Error(`Clinical lists load failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientMessages(patientId: string, signal?: AbortSignal): Promise<PatientMessagesResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(patientId.trim())}`, { signal })
  if (!response.ok) {
    throw new Error(`Patient messages load failed with ${response.status}`)
  }

  return response.json()
}

export async function getProcedureResults(patientId: string, signal?: AbortSignal): Promise<ProcedureResultsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/${encodeURIComponent(patientId.trim())}`, { signal })
  if (!response.ok) {
    throw new Error(`Procedure results load failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientBilling(patientId: string, signal?: AbortSignal): Promise<PatientBillingResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/${encodeURIComponent(patientId.trim())}`, { signal })
  if (!response.ok) {
    throw new Error(`Patient billing load failed with ${response.status}`)
  }

  return response.json()
}

export async function getAdministrationDirectory(signal?: AbortSignal): Promise<AdministrationDirectoryResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/directory`, { signal })
  if (!response.ok) {
    throw new Error(`Administration directory load failed with ${response.status}`)
  }

  return response.json()
}

export async function getOperationalReports(signal?: AbortSignal): Promise<OperationalReportsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/reports/operational`, { signal })
  if (!response.ok) {
    throw new Error(`Operational reports load failed with ${response.status}`)
  }

  return response.json()
}
