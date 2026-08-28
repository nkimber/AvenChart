// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

import type { TelehealthRequestStatus } from './api.ts'

const MINIMUM_DELAY_SECONDS = 2
const MAXIMUM_DELAY_SECONDS = 30
const JITTER_FRACTION = 0.1

export function shouldPollPatientQueueStatus(status: TelehealthRequestStatus) {
  return ['OperationalReview', 'Queued', 'Reserved', 'Connecting'].includes(status)
}

export function queuePollDelayMilliseconds(
  refreshAfterSeconds: number,
  consecutiveFailures: number,
  random: () => number = Math.random,
) {
  const requested = Number.isFinite(refreshAfterSeconds) ? refreshAfterSeconds : 5
  const boundedRefresh = Math.min(MAXIMUM_DELAY_SECONDS, Math.max(MINIMUM_DELAY_SECONDS, requested))
  const failures = Math.min(8, Math.max(0, Math.trunc(consecutiveFailures)))
  const backedOff = Math.min(MAXIMUM_DELAY_SECONDS, boundedRefresh * (2 ** failures))
  const randomValue = Math.min(1, Math.max(0, random()))
  const jitterMultiplier = 1 - JITTER_FRACTION + (randomValue * JITTER_FRACTION * 2)
  return Math.round(Math.min(MAXIMUM_DELAY_SECONDS, Math.max(MINIMUM_DELAY_SECONDS, backedOff * jitterMultiplier)) * 1000)
}
