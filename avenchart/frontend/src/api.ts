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
