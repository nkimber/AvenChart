// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, expect, it } from 'vitest'
import { canCancelPatientTelehealthRequest } from './requestCancellation.ts'

describe('canCancelPatientTelehealthRequest', () => {
  it('permits cancellation while the request is ready in the practice queue', () => {
    expect(canCancelPatientTelehealthRequest('Queued')).toBe(true)
  })

  it.each(['Reserved', 'Connecting', 'InConsultation', 'WrapUp', 'Closed'] as const)(
    'does not permit cancellation once clinician work may exist: %s',
    (status) => expect(canCancelPatientTelehealthRequest(status)).toBe(false),
  )
})
