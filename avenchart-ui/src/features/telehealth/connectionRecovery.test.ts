// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, expect, it } from 'vitest'
import { connectionWasReturnedToQueue } from './connectionRecovery.ts'

describe('connectionWasReturnedToQueue', () => {
  it('recognizes only the non-clinical Connecting-to-Queued recovery transition', () => {
    expect(connectionWasReturnedToQueue('Connecting', 'Queued')).toBe(true)
    expect(connectionWasReturnedToQueue('Reserved', 'Queued')).toBe(false)
    expect(connectionWasReturnedToQueue('Connecting', 'InConsultation')).toBe(false)
    expect(connectionWasReturnedToQueue(null, 'Queued')).toBe(false)
  })
})
