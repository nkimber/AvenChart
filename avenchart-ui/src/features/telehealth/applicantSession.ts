// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

const storageKey = 'avenchart-ui.telehealthProspectiveApplicant'

export type TelehealthApplicantSession = {
  applicantId: string
  applicantAccessKey: string
}

export function createApplicantAccessKey() {
  const bytes = new Uint8Array(32)
  globalThis.crypto.getRandomValues(bytes)
  return Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('')
}

export function loadApplicantSession(): TelehealthApplicantSession | null {
  try {
    const value = sessionStorage.getItem(storageKey)
    if (!value) return null
    const parsed = JSON.parse(value) as Partial<TelehealthApplicantSession>
    return typeof parsed.applicantId === 'string'
      && typeof parsed.applicantAccessKey === 'string'
      && parsed.applicantAccessKey.length >= 43
      ? { applicantId: parsed.applicantId, applicantAccessKey: parsed.applicantAccessKey }
      : null
  } catch {
    return null
  }
}

export function saveApplicantSession(session: TelehealthApplicantSession) {
  sessionStorage.setItem(storageKey, JSON.stringify(session))
}

export function clearApplicantSession() {
  sessionStorage.removeItem(storageKey)
}

