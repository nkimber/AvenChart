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
}

export type PortalSession = {
  sessionId: string
  username: string
  portalUsername: string
  displayName: string
}

const CLINICIAN_KEY = 'avenchart-ui.clinicianSession'
const PORTAL_KEY = 'avenchart-ui.portalSession'

export function saveClinicianSession(session: ClinicianSession) {
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
