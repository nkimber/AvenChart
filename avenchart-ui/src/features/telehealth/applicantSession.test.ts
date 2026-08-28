// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, expect, it } from 'vitest'
import { clearApplicantSession, createApplicantAccessKey, loadApplicantSession, saveApplicantSession } from './applicantSession.ts'

describe('prospective applicant session boundary', () => {
  it('generates a 256-bit browser credential and keeps it only in session storage', () => {
    const key = createApplicantAccessKey()
    expect(key).toMatch(/^[0-9a-f]{64}$/)

    saveApplicantSession({ applicantId: 'applicant-1', applicantAccessKey: key })
    expect(loadApplicantSession()).toEqual({ applicantId: 'applicant-1', applicantAccessKey: key })
    expect(localStorage.length).toBe(0)

    clearApplicantSession()
    expect(loadApplicantSession()).toBeNull()
  })

  it('rejects malformed stored credentials', () => {
    sessionStorage.setItem('avenchart-ui.telehealthProspectiveApplicant', JSON.stringify({ applicantId: 'applicant-1', applicantAccessKey: 'short' }))
    expect(loadApplicantSession()).toBeNull()
  })
})

