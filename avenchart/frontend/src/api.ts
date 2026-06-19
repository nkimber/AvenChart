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

export type PatientInsuranceItem = {
  id: string
  type?: string | null
  provider?: string | null
  planName?: string | null
  policyNumber?: string | null
  groupNumber?: string | null
  relationship?: string | null
}

export type PatientInsuranceMutationInput = {
  type: string
  provider: string
  planName: string
  policyNumber: string
  groupNumber: string
  relationship: string
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
  insurance: PatientInsuranceItem[]
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
  billingNote?: string | null
  vitals?: EncounterVitals | null
  soapNote?: EncounterSoapNote | null
}

export type EncounterCreateInput = {
  patientId: string
  providerId?: number | null
  dateTime: string
  reason: string
  facilityId?: number | null
  billingFacilityId?: number | null
  billingNote?: string | null
}

export type EncounterUpdateInput = {
  reason: string
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
  contentPreview?: string | null
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
  mimetype?: string | null
  sizeBytes?: number | null
  pages?: number | null
  encounter?: number | null
  storageMethod?: string | null
  url?: string | null
  hash?: string | null
  documentationOf?: string | null
  notes?: string | null
  content: string
  contentBase64?: string | null
  isBinary: boolean
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
  reportDate: string
  status?: string | null
  reviewStatus?: string | null
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
  reports: ProcedureReportItem[]
}

export type ProcedureOrderCounts = {
  orders: number
  completedOrders: number
  scheduledOrders: number
  reportlessOrders: number
  futureScheduledOrders: number
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

export type ProcedureReportCreateInput = {
  orderId: number
  dateCollected: string
  dateReport: string
  specimenNumber: string
  reportStatus: string
  reviewStatus: string
  notes: string
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
  codeText?: string | null
  fee?: number | null
  justify?: string | null
  units: number
  billed: number
  activity: number
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

export type BillingLineCreateInput = {
  patientId: string
  providerId?: number | null
  encounter: number
  billingDate: string
  codeType: string
  code: string
  codeText: string
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

export async function createPatientInsurance(
  patientId: string,
  insurance: PatientInsuranceMutationInput,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/${encodeURIComponent(patientId)}/insurance`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(insurance),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient insurance create failed with ${response.status}`)
  }

  return response.json()
}

export async function updatePatientInsurance(
  insuranceId: string,
  insurance: PatientInsuranceMutationInput,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/insurance/${encodeURIComponent(insuranceId)}`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(insurance),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient insurance update failed with ${response.status}`)
  }

  return response.json()
}

export async function deletePatientInsurance(
  insuranceId: string,
  signal?: AbortSignal,
): Promise<PatientChartSummary> {
  const response = await fetch(`${apiBaseUrl}/api/patients/insurance/${encodeURIComponent(insuranceId)}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient insurance delete failed with ${response.status}`)
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

export async function createEncounter(
  encounter: EncounterCreateInput,
  signal?: AbortSignal,
): Promise<EncounterDetail> {
  const response = await fetch(`${apiBaseUrl}/api/encounters`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(encounter),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Encounter create failed with ${response.status}`)
  }

  return response.json()
}

export async function updateEncounter(
  encounter: number,
  update: EncounterUpdateInput,
  signal?: AbortSignal,
): Promise<EncounterDetail> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Encounter update failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteEncounter(encounter: number, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Encounter delete failed with ${response.status}`)
  }
}

export async function createEncounterVitals(
  encounter: number,
  vitals: EncounterVitalsCreateInput,
  signal?: AbortSignal,
): Promise<EncounterFormMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/vitals`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(vitals),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Encounter vitals create failed with ${response.status}`)
  }

  return response.json()
}

export async function createEncounterSoapNote(
  encounter: number,
  soapNote: EncounterSoapNoteCreateInput,
  signal?: AbortSignal,
): Promise<EncounterFormMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/encounters/${encounter}/soap-notes`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(soapNote),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Encounter SOAP note create failed with ${response.status}`)
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

export async function createClinicalAllergy(
  allergy: ClinicalAllergyCreateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/allergies`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(allergy),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical allergy create failed with ${response.status}`)
  }

  return response.json()
}

export async function createClinicalProblem(
  problem: ClinicalProblemCreateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/problems`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(problem),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical problem create failed with ${response.status}`)
  }

  return response.json()
}

export async function deactivateClinicalProblem(
  problemId: string,
  update: ClinicalListDeactivateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/problems/${encodeURIComponent(problemId)}/deactivate`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical problem deactivate failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteClinicalProblem(problemId: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/problems/${encodeURIComponent(problemId)}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical problem delete failed with ${response.status}`)
  }
}

export async function createClinicalMedication(
  medication: ClinicalMedicationCreateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/medications`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(medication),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical medication create failed with ${response.status}`)
  }

  return response.json()
}

export async function deactivateClinicalMedication(
  medicationId: string,
  update: ClinicalListDeactivateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/medications/${encodeURIComponent(medicationId)}/deactivate`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical medication deactivate failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteClinicalMedication(medicationId: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/medications/${encodeURIComponent(medicationId)}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical medication delete failed with ${response.status}`)
  }
}

export async function deactivateClinicalAllergy(
  allergyId: string,
  update: ClinicalListDeactivateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/allergies/${encodeURIComponent(allergyId)}/deactivate`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical allergy deactivate failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteClinicalAllergy(allergyId: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/allergies/${encodeURIComponent(allergyId)}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical allergy delete failed with ${response.status}`)
  }
}

export async function createClinicalPrescription(
  prescription: ClinicalPrescriptionCreateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/prescriptions`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(prescription),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical prescription create failed with ${response.status}`)
  }

  return response.json()
}

export async function deactivateClinicalPrescription(
  prescriptionId: string,
  update: ClinicalPrescriptionDeactivateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}/deactivate`,
    {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(update),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(`Clinical prescription deactivate failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteClinicalPrescription(prescriptionId: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/prescriptions/${encodeURIComponent(prescriptionId)}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical prescription delete failed with ${response.status}`)
  }
}

export async function createClinicalImmunization(
  immunization: ClinicalImmunizationCreateInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/clinical-lists/immunizations`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(immunization),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Clinical immunization create failed with ${response.status}`)
  }

  return response.json()
}

export async function markClinicalImmunizationEnteredInError(
  immunizationId: number,
  update: ClinicalImmunizationErrorInput,
  signal?: AbortSignal,
): Promise<ClinicalListMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/clinical-lists/immunizations/${encodeURIComponent(String(immunizationId))}/entered-in-error`,
    {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(update),
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(`Clinical immunization entered-in-error update failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteClinicalImmunization(immunizationId: number, signal?: AbortSignal): Promise<void> {
  const response = await fetch(
    `${apiBaseUrl}/api/clinical-lists/immunizations/${encodeURIComponent(String(immunizationId))}`,
    {
      method: 'DELETE',
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(`Clinical immunization delete failed with ${response.status}`)
  }
}

export async function getPatientMessages(patientId: string, signal?: AbortSignal): Promise<PatientMessagesResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(patientId.trim())}`, { signal })
  if (!response.ok) {
    throw new Error(`Patient messages load failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientDocuments(patientId: string, signal?: AbortSignal): Promise<PatientDocumentsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(patientId.trim())}`, { signal })
  if (!response.ok) {
    throw new Error(`Patient documents load failed with ${response.status}`)
  }

  return response.json()
}

export async function getPatientDocumentContent(
  documentId: number,
  signal?: AbortSignal,
): Promise<PatientDocumentContentResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/content`, {
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient document content load failed with ${response.status}`)
  }

  return response.json()
}

export function getPatientDocumentDownloadUrl(documentId: number) {
  return `${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/download`
}

export async function createPatientDocument(
  document: PatientDocumentCreateInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient document create failed with ${response.status}`)
  }

  return response.json()
}

export async function createPatientBinaryDocument(
  document: PatientDocumentBinaryCreateInput,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/binary`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(document),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Binary patient document create failed with ${response.status}`)
  }

  return response.json()
}

export async function softDeletePatientDocument(
  documentId: number,
  signal?: AbortSignal,
): Promise<PatientDocumentMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}/soft-delete`, {
    method: 'PUT',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient document archive failed with ${response.status}`)
  }

  return response.json()
}

export async function deletePatientDocument(documentId: number, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/documents/${encodeURIComponent(String(documentId))}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient document delete failed with ${response.status}`)
  }
}

export async function createPatientMessage(
  message: PatientMessageCreateInput,
  signal?: AbortSignal,
): Promise<PatientMessageMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(message),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient message create failed with ${response.status}`)
  }

  return response.json()
}

export async function updatePatientMessageStatus(
  messageId: string,
  update: PatientMessageStatusUpdateInput,
  signal?: AbortSignal,
): Promise<PatientMessageMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(messageId)}/status`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(update),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient message update failed with ${response.status}`)
  }

  return response.json()
}

export async function softDeletePatientMessage(
  messageId: string,
  signal?: AbortSignal,
): Promise<PatientMessageMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(messageId)}/soft-delete`, {
    method: 'PUT',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient message archive failed with ${response.status}`)
  }

  return response.json()
}

export async function deletePatientMessage(messageId: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/messages/${encodeURIComponent(messageId)}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Patient message delete failed with ${response.status}`)
  }
}

export async function getProcedureResults(patientId: string, signal?: AbortSignal): Promise<ProcedureResultsResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/${encodeURIComponent(patientId.trim())}`, { signal })
  if (!response.ok) {
    throw new Error(`Procedure results load failed with ${response.status}`)
  }

  return response.json()
}

export async function createProcedureOrder(
  input: ProcedureOrderCreateInput,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/orders`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Procedure order create failed with ${response.status}`)
  }

  return response.json()
}

export async function updateProcedureOrderStatus(
  orderId: number,
  input: ProcedureOrderStatusUpdateInput,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/orders/${encodeURIComponent(String(orderId))}/status`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Procedure order status update failed with ${response.status}`)
  }

  return response.json()
}

export async function createProcedureReport(
  input: ProcedureReportCreateInput,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/reports`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Procedure report create failed with ${response.status}`)
  }

  return response.json()
}

export async function createProcedureResult(
  input: ProcedureResultCreateInput,
  signal?: AbortSignal,
): Promise<ProcedureMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/results`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Procedure result create failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteProcedureOrder(orderId: number, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/procedures/orders/${encodeURIComponent(String(orderId))}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Procedure order delete failed with ${response.status}`)
  }
}

export async function getPatientBilling(patientId: string, signal?: AbortSignal): Promise<PatientBillingResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/${encodeURIComponent(patientId.trim())}`, { signal })
  if (!response.ok) {
    throw new Error(`Patient billing load failed with ${response.status}`)
  }

  return response.json()
}

export async function createBillingLine(
  input: BillingLineCreateInput,
  signal?: AbortSignal,
): Promise<BillingLineMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/lines`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Billing line create failed with ${response.status}`)
  }

  return response.json()
}

export async function updateBillingLineStatus(
  billingLineId: string,
  input: BillingLineStatusUpdateInput,
  signal?: AbortSignal,
): Promise<BillingLineMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/billing/lines/${encodeURIComponent(billingLineId)}/status`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Billing line status update failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteBillingLine(billingLineId: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/billing/lines/${encodeURIComponent(billingLineId)}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Billing line delete failed with ${response.status}`)
  }
}

export async function getAdministrationDirectory(signal?: AbortSignal): Promise<AdministrationDirectoryResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/directory`, { signal })
  if (!response.ok) {
    throw new Error(`Administration directory load failed with ${response.status}`)
  }

  return response.json()
}

export async function createAdministrationUser(
  input: AdministrationUserMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationUserMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/users`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Administration user create failed with ${response.status}`)
  }

  return response.json()
}

export async function updateAdministrationUser(
  userId: number,
  input: AdministrationUserMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationUserMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/users/${userId}`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Administration user update failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteAdministrationUser(userId: number, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/administration/users/${userId}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Administration user delete failed with ${response.status}`)
  }
}

export async function createAdministrationFacility(
  input: AdministrationFacilityMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationFacilityMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/facilities`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Administration facility create failed with ${response.status}`)
  }

  return response.json()
}

export async function updateAdministrationFacility(
  facilityId: number,
  input: AdministrationFacilityMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationFacilityMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/facilities/${facilityId}`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Administration facility update failed with ${response.status}`)
  }

  return response.json()
}

export async function deleteAdministrationFacility(facilityId: number, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/administration/facilities/${facilityId}`, {
    method: 'DELETE',
    signal,
  })
  if (!response.ok) {
    throw new Error(`Administration facility delete failed with ${response.status}`)
  }
}

export async function grantAdministrationAccessPermission(
  input: AdministrationAccessPermissionMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationAccessPermissionMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/access-control/group-permissions`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Administration access permission grant failed with ${response.status}`)
  }

  return response.json()
}

export async function revokeAdministrationAccessPermission(
  input: Pick<AdministrationAccessPermissionMutationInput, 'groupValue' | 'sectionValue' | 'permissionValue'>,
  signal?: AbortSignal,
): Promise<AdministrationAccessPermissionMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/administration/access-control/group-permissions/${encodeURIComponent(input.groupValue)}/${encodeURIComponent(input.sectionValue)}/${encodeURIComponent(input.permissionValue)}`,
    {
      method: 'DELETE',
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(`Administration access permission revoke failed with ${response.status}`)
  }

  return response.json()
}

export async function grantAdministrationAccessUserMembership(
  input: AdministrationAccessUserMembershipMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationAccessUserMembershipMutationResponse> {
  const response = await fetch(`${apiBaseUrl}/api/administration/access-control/user-memberships`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
    signal,
  })
  if (!response.ok) {
    throw new Error(`Administration access user membership grant failed with ${response.status}`)
  }

  return response.json()
}

export async function revokeAdministrationAccessUserMembership(
  input: AdministrationAccessUserMembershipMutationInput,
  signal?: AbortSignal,
): Promise<AdministrationAccessUserMembershipMutationResponse> {
  const response = await fetch(
    `${apiBaseUrl}/api/administration/access-control/user-memberships/${encodeURIComponent(input.userValue)}/${encodeURIComponent(input.groupValue)}`,
    {
      method: 'DELETE',
      signal,
    },
  )
  if (!response.ok) {
    throw new Error(`Administration access user membership revoke failed with ${response.status}`)
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

export function getOperationalReportsCsvUrl() {
  return `${apiBaseUrl}/api/reports/operational/export`
}
