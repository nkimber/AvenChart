// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

// Tiny session-storage helpers. Two independent session "slots" so a
// clinician sign-in and a patient-portal sign-in never collide.

export type ClinicianSession = {
  sessionId: string
  username: string
  displayName: string
  role: string
  staffId?: number | null
  facilityId?: number | null
  purposeOfUse?: string
  facilities?: ClinicianAccessFacility[]
  purposes?: string[]
}

export type ClinicianAccessFacility = {
  facilityId: number
  code: string
  name: string
  isDefault: boolean
}

export type PortalSession = {
  sessionId: string
  username: string
  portalUsername: string
  displayName: string
}

const CLINICIAN_KEY = 'avenchart-ui.clinicianSession'
const PORTAL_KEY = 'avenchart-ui.portalSession'
export const CLINICIAN_EPHEMERAL_STORAGE_PREFIX = 'avenchart-ui.clinician.'
export const CLINICIAN_ENCOUNTER_TEMPLATE_KEY = `${CLINICIAN_EPHEMERAL_STORAGE_PREFIX}encounterTemplates`
const LEGACY_ENCOUNTER_TEMPLATE_KEY = 'encounter-templates'

function clearClinicianEphemeralBrowserData() {
  for (let index = sessionStorage.length - 1; index >= 0; index -= 1) {
    const key = sessionStorage.key(index)
    if (key?.startsWith(CLINICIAN_EPHEMERAL_STORAGE_PREFIX)) {
      sessionStorage.removeItem(key)
    }
  }
  // Phase 1 stored complete SOAP text in persistent local storage. It is not
  // safe to carry that content into another clinician's browser session.
  localStorage.removeItem(LEGACY_ENCOUNTER_TEMPLATE_KEY)
}

export function saveClinicianSession(session: ClinicianSession) {
  clearClinicianEphemeralBrowserData()
  sessionStorage.setItem(CLINICIAN_KEY, JSON.stringify(session))
}

/** Updates the selected facility or purpose without treating it as a new sign-in. */
export function updateClinicianSession(session: ClinicianSession) {
  sessionStorage.setItem(CLINICIAN_KEY, JSON.stringify(session))
}

export function loadClinicianSession(): ClinicianSession | null {
  const raw = sessionStorage.getItem(CLINICIAN_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as ClinicianSession
  } catch {
    return null
  }
}

export function clearClinicianSession() {
  clearClinicianEphemeralBrowserData()
  sessionStorage.removeItem(CLINICIAN_KEY)
}

export function savePortalSession(session: PortalSession) {
  sessionStorage.setItem(PORTAL_KEY, JSON.stringify(session))
}

export function loadPortalSession(): PortalSession | null {
  const raw = sessionStorage.getItem(PORTAL_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as PortalSession
  } catch {
    return null
  }
}

export function clearPortalSession() {
  sessionStorage.removeItem(PORTAL_KEY)
}
