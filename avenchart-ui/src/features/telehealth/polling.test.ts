// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import { describe, expect, it } from 'vitest'
import { queuePollDelayMilliseconds, shouldPollPatientQueueStatus } from './polling.ts'

describe('telehealth patient queue polling', () => {
  it('backs off from the server interval and caps failures at thirty seconds', () => {
    expect(queuePollDelayMilliseconds(5, 0, () => 0.5)).toBe(5_000)
    expect(queuePollDelayMilliseconds(5, 1, () => 0.5)).toBe(10_000)
    expect(queuePollDelayMilliseconds(5, 8, () => 0.5)).toBe(30_000)
  })

  it('applies bounded jitter without escaping the safety limits', () => {
    expect(queuePollDelayMilliseconds(5, 0, () => 0)).toBe(4_500)
    expect(queuePollDelayMilliseconds(5, 0, () => 1)).toBe(5_500)
    expect(queuePollDelayMilliseconds(1, 0, () => 0)).toBe(2_000)
    expect(queuePollDelayMilliseconds(30, 0, () => 1)).toBe(30_000)
  })

  it('polls only patient-visible review and queue states', () => {
    expect(shouldPollPatientQueueStatus('OperationalReview')).toBe(true)
    expect(shouldPollPatientQueueStatus('Queued')).toBe(true)
    expect(shouldPollPatientQueueStatus('Reserved')).toBe(true)
    expect(shouldPollPatientQueueStatus('InConsultation')).toBe(true)
    expect(shouldPollPatientQueueStatus('WrapUp')).toBe(false)
    expect(shouldPollPatientQueueStatus('Closed')).toBe(false)
    expect(shouldPollPatientQueueStatus('Redirected')).toBe(false)
    expect(shouldPollPatientQueueStatus('Verification')).toBe(false)
  })
})
