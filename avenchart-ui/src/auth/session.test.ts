// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { beforeEach, describe, expect, it } from 'vitest'
import {
  CLINICIAN_ENCOUNTER_TEMPLATE_KEY,
  clearClinicianSession,
  saveClinicianSession,
  savePortalSession,
} from './session.ts'

describe('clinician browser-session cleanup', () => {
  beforeEach(() => {
    window.sessionStorage.clear()
    window.localStorage.clear()
  })

  it('removes a prior clinician template and legacy SOAP storage when a new clinician signs in', () => {
    window.sessionStorage.setItem(CLINICIAN_ENCOUNTER_TEMPLATE_KEY, '[{"subjective":"prior session"}]')
    window.localStorage.setItem('encounter-templates', '[{"subjective":"legacy session"}]')

    saveClinicianSession({
      sessionId: 'new-session',
      username: 'clinician',
      displayName: 'Clinician',
      role: 'staff',
    })

    expect(window.sessionStorage.getItem(CLINICIAN_ENCOUNTER_TEMPLATE_KEY)).toBeNull()
    expect(window.localStorage.getItem('encounter-templates')).toBeNull()
  })

  it('clears clinician-only ephemeral data without ending a portal session', () => {
    savePortalSession({
      sessionId: 'portal-session',
      username: 'patient',
      portalUsername: 'patient.portal',
      displayName: 'Patient',
    })
    window.sessionStorage.setItem(CLINICIAN_ENCOUNTER_TEMPLATE_KEY, '[{"subjective":"current session"}]')

    clearClinicianSession()

    expect(window.sessionStorage.getItem(CLINICIAN_ENCOUNTER_TEMPLATE_KEY)).toBeNull()
    expect(window.sessionStorage.getItem('avenchart-ui.portalSession')).not.toBeNull()
  })
})
